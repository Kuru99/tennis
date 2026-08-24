using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using PrideCourt.AI;
using PrideCourt.Cards;
using PrideCourt.Domain;
using PrideCourt.Gameplay;
using PrideCourt.Input;
using PrideCourt.Presentation;
using UnityEngine;

namespace PrideCourt.Networking
{
    public enum LanSessionRole
    {
        Offline,
        Hosting,
        Joining,
        Host,
        Guest,
        Error
    }

    public sealed class LanBattleSessionController : MonoBehaviour
    {
        private const float SnapshotInterval = 1f / 30f;
        private const float CommandInterval = 1f / 30f;

        [SerializeField] private TennisMatchController match;
        [SerializeField] private TennisAthleteController nearAthlete;
        [SerializeField] private TennisAthleteController farAthlete;
        [SerializeField] private TennisBallController ball;

        private readonly LanDiscoveryService discovery = new LanDiscoveryService();
        private readonly Dictionary<string, LanDiscoveredHost> hosts = new Dictionary<string, LanDiscoveredHost>();
        private KeyboardMouseCommandSource localInput;
        private CpuCommandSource cpuInput;
        private RemoteCommandSource remoteInput;
        private CardLoadoutController nearCards;
        private CardLoadoutController farCards;
        private TennisCameraController cameraController;
        private TcpListener listener;
        private Task<TcpClient> acceptTask;
        private TcpClient joiningClient;
        private Task joinTask;
        private IBattleTransport peer;
        private EosOnlineLobbyClient onlineLobby;
        private bool onlineSession;
        private AthleteIdentity localIdentity;
        private CardId[] localDeck = Array.Empty<CardId>();
        private float nextSnapshotAt;
        private float nextCommandAt;
        private uint snapshotSequence;
        private uint lastAppliedSequence;
        private bool localSettingsOpen;
        private bool remoteSettingsOpen;
        private int hostedGamesToWin = MatchScore.DefaultGamesToWin;

        public LanSessionRole Role { get; private set; } = LanSessionRole.Offline;
        public string Status { get; private set; } = "未接続";
        public string ErrorMessage { get; private set; } = string.Empty;
        public IReadOnlyList<LanDiscoveredHost> Hosts => hosts.Values.OrderBy(host => host.RoomName).ToArray();
        public IReadOnlyList<string> LocalAddresses => LanDiscoveryService.GetLocalAddresses();
        public bool IsBusy => Role == LanSessionRole.Hosting || Role == LanSessionRole.Joining;
        public bool IsConnected => Role == LanSessionRole.Host || Role == LanSessionRole.Guest;
        public bool IsHost => Role == LanSessionRole.Host;
        public bool IsOnlineSession => onlineSession;
        public bool IsOnlineConfigured => OnlineLobby.IsConfigured;
        public EosOnlineLobbyState OnlineState => OnlineLobby.State;
        public string OnlineConfigurationStatus => OnlineLobby.ConfigurationStatus;
        public string OnlineRoomCode => OnlineLobby.RoomCode;

        public void Configure(TennisMatchController configuredMatch, TennisAthleteController configuredNear,
            TennisAthleteController configuredFar, TennisBallController configuredBall)
        {
            match = configuredMatch;
            nearAthlete = configuredNear;
            farAthlete = configuredFar;
            ball = configuredBall;
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (match == null) match = GetComponent<TennisMatchController>();
            if (match == null) return;
            if (nearAthlete == null || farAthlete == null)
            {
                TennisAthleteController[] athletes = FindObjectsByType<TennisAthleteController>(FindObjectsInactive.Exclude);
                nearAthlete = athletes.FirstOrDefault(athlete => athlete.Side == CourtSide.Near);
                farAthlete = athletes.FirstOrDefault(athlete => athlete.Side == CourtSide.Far);
            }
            if (ball == null) ball = FindAnyObjectByType<TennisBallController>();
            if (nearAthlete == null || farAthlete == null || ball == null) return;
            localInput = nearAthlete.GetComponent<KeyboardMouseCommandSource>();
            cpuInput = farAthlete.GetComponent<CpuCommandSource>();
            remoteInput = farAthlete.GetComponent<RemoteCommandSource>();
            if (remoteInput == null) remoteInput = farAthlete.gameObject.AddComponent<RemoteCommandSource>();
            nearCards = nearAthlete.GetComponent<CardLoadoutController>();
            farCards = farAthlete.GetComponent<CardLoadoutController>();
            cameraController = FindAnyObjectByType<TennisCameraController>();
        }

        private void Update()
        {
            while (discovery.TryDequeue(out LanDiscoveredHost host)) hosts[host.Key] = host;
            foreach (string stale in hosts.Where(pair => (DateTime.UtcNow - pair.Value.LastSeenUtc).TotalSeconds > 8d)
                         .Select(pair => pair.Key).ToArray()) hosts.Remove(stale);

            PollHosting();
            PollJoining();
            PollOnlineTransport();
            PollPeer();

            if (Role == LanSessionRole.Host && peer != null && peer.IsConnected && match != null && match.HasStarted &&
                Time.unscaledTime >= nextSnapshotAt)
            {
                nextSnapshotAt = Time.unscaledTime + SnapshotInterval;
                peer.Send(LanPacketType.Snapshot, LanBattleProtocol.EncodeSnapshot(match.CaptureLanState(++snapshotSequence)));
            }
            else if (Role == LanSessionRole.Guest && peer != null && peer.IsConnected && localInput != null &&
                     Time.unscaledTime >= nextCommandAt)
            {
                nextCommandAt = Time.unscaledTime + CommandInterval;
                TennisCommand command = NormalizeClientCommand(localInput.ReadCommand());
                peer.Send(LanPacketType.Command, LanBattleProtocol.EncodeCommand(command));
            }

            if (peer != null && !peer.IsConnected && (IsConnected || IsBusy))
            {
                Fail(string.IsNullOrEmpty(peer.LastError) ? "対戦相手との接続が切れました。" : peer.LastError);
            }
        }

        public void RefreshHosts()
        {
            hosts.Clear();
            discovery.Scan();
            Status = "同じLANのコートを検索中…";
        }

        public bool StartHosting(AthleteIdentity identity, IReadOnlyList<CardId> deck, int gamesToWin)
        {
            if (!ValidateDeck(deck)) return Fail("16枚のデッキを用意してください。");
            if (!MatchScore.IsSupportedGamesToWin(gamesToWin))
                return Fail("ゲーム先取数は1から3の間で選んでください。");
            StopTransport();
            PrepareAuthoritativeWorld();
            hostedGamesToWin = gamesToWin;
            localIdentity = identity;
            localDeck = deck.ToArray();
            nearAthlete.SetIdentity(identity);
            nearCards.RebuildDeck(localDeck);
            farAthlete.SetCommandSource(remoteInput);
            farCards.SetCpuControlled(false);
            remoteInput.Clear();
            try
            {
                listener = new TcpListener(IPAddress.Any, LanBattleProtocol.GamePort);
                listener.Start(1);
                acceptTask = listener.AcceptTcpClientAsync();
                discovery.StartHost(SystemInfo.deviceName + " COURT", hostedGamesToWin);
                Role = LanSessionRole.Hosting;
                Status = hostedGamesToWin + "ゲーム先取でコートを開設しました。相手を待っています。";
                ErrorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return Fail("ホストを開始できません: " + exception.Message);
            }
        }

        public bool Join(LanDiscoveredHost host, AthleteIdentity identity, IReadOnlyList<CardId> deck)
        {
            return host != null && JoinAddress(host.Address, host.Port, identity, deck);
        }

        public bool JoinAddress(string address, int port, AthleteIdentity identity, IReadOnlyList<CardId> deck)
        {
            if (!ValidateDeck(deck)) return Fail("16枚のデッキを用意してください。");
            if (!IPAddress.TryParse((address ?? string.Empty).Trim(), out IPAddress ip) ||
                ip.AddressFamily != AddressFamily.InterNetwork)
                return Fail("IPv4アドレスを確認してください。");
            StopTransport();
            PrepareReplicaWorld();
            localIdentity = identity;
            localDeck = deck.ToArray();
            try
            {
                joiningClient = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
                joinTask = joiningClient.ConnectAsync(ip, port);
                Role = LanSessionRole.Joining;
                Status = address + " に接続中…";
                ErrorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return Fail("参加を開始できません: " + exception.Message);
            }
        }

        public bool StartOnlineHosting(AthleteIdentity identity, IReadOnlyList<CardId> deck)
        {
            if (!ValidateDeck(deck)) return Fail("16枚のデッキを用意してください。");
            StopTransport();
            PrepareAuthoritativeWorld();
            onlineSession = true;
            hostedGamesToWin = MatchScore.DefaultGamesToWin;
            localIdentity = identity;
            localDeck = deck.ToArray();
            nearAthlete.SetIdentity(identity);
            nearCards.RebuildDeck(localDeck);
            farAthlete.SetCommandSource(remoteInput);
            farCards.SetCpuControlled(false);
            remoteInput.Clear();
            Role = LanSessionRole.Hosting;
            if (!OnlineLobby.Host()) return Fail(OnlineLobby.ErrorMessage);
            Status = OnlineLobby.Status;
            ErrorMessage = string.Empty;
            return true;
        }

        public bool JoinOnline(string roomCode, AthleteIdentity identity, IReadOnlyList<CardId> deck)
        {
            if (!ValidateDeck(deck)) return Fail("16枚のデッキを用意してください。");
            StopTransport();
            PrepareReplicaWorld();
            onlineSession = true;
            localIdentity = identity;
            localDeck = deck.ToArray();
            Role = LanSessionRole.Joining;
            if (!OnlineLobby.Join(roomCode)) return Fail(OnlineLobby.ErrorMessage);
            Status = OnlineLobby.Status;
            ErrorMessage = string.Empty;
            return true;
        }

        public void PrepareSolo()
        {
            StopTransport();
            PrepareAuthoritativeWorld();
            hostedGamesToWin = MatchScore.DefaultGamesToWin;
            nearAthlete.SetCommandSource(localInput);
            farAthlete.SetCommandSource(cpuInput);
            nearCards.SetCpuControlled(false);
            farCards.SetCpuControlled(true);
            cpuInput?.Configure(farAthlete, nearAthlete, ball, match);
            Role = LanSessionRole.Offline;
            Status = "未接続";
            ErrorMessage = string.Empty;
        }

        public void Disconnect()
        {
            peer?.Send(LanPacketType.Disconnect, Array.Empty<byte>());
            StopTransport();
            PrepareAuthoritativeWorld();
            match?.ReturnToSetup();
            Role = LanSessionRole.Offline;
            Status = "切断しました。";
        }

        public bool StartRematch()
        {
            if (Role != LanSessionRole.Host || match == null) return false;
            match.StartNewMatch(match.Score.GamesToWin);
            return true;
        }

        public bool SetSettingsMenuOpen(bool open)
        {
            ResolveReferences();
            if (match == null || !match.HasStarted) return false;
            if (open && !SettingsAvailabilityPolicy.CanOpen(IsConnected, match.HasStarted, match.Phase,
                    match.ServeRestrictionsActive))
                return false;

            localSettingsOpen = open;
            if (Role == LanSessionRole.Host)
            {
                RefreshHostSettingsPause();
                return true;
            }
            if (Role == LanSessionRole.Guest)
            {
                match.SetSettingsPaused(open);
                peer?.Send(LanPacketType.SettingsPause, LanBattleProtocol.EncodeSettingsPause(open));
                return true;
            }

            match.SetSettingsPaused(open);
            return true;
        }

        private void PollHosting()
        {
            if (Role != LanSessionRole.Hosting || acceptTask == null || !acceptTask.IsCompleted) return;
            try
            {
                TcpClient accepted = acceptTask.GetAwaiter().GetResult();
                listener.Stop();
                listener = null;
                acceptTask = null;
                peer = new LanPeerConnection(accepted);
                Status = "参加者を確認中…";
            }
            catch (Exception exception)
            {
                Fail("参加受付に失敗しました: " + exception.Message);
            }
        }

        private void PollJoining()
        {
            if (Role != LanSessionRole.Joining || joinTask == null || !joinTask.IsCompleted) return;
            try
            {
                joinTask.GetAwaiter().GetResult();
                peer = new LanPeerConnection(joiningClient);
                joiningClient = null;
                joinTask = null;
                peer.Send(LanPacketType.Hello, LanBattleProtocol.EncodeHello(localIdentity, localDeck));
                Status = "ホストの応答を待っています…";
            }
            catch (Exception exception)
            {
                Fail("コートに参加できません: " + exception.Message);
            }
        }

        private void PollOnlineTransport()
        {
            if (!onlineSession || (Role != LanSessionRole.Hosting && Role != LanSessionRole.Joining)) return;
            Status = OnlineLobby.Status;
            if (OnlineLobby.State == EosOnlineLobbyState.Error ||
                OnlineLobby.State == EosOnlineLobbyState.Unconfigured)
            {
                Fail(string.IsNullOrEmpty(OnlineLobby.ErrorMessage) ? OnlineLobby.Status : OnlineLobby.ErrorMessage);
                return;
            }
            if (peer != null || !OnlineLobby.TryTakeTransport(out IBattleTransport transport)) return;
            peer = transport;
            if (Role == LanSessionRole.Joining)
            {
                peer.Send(LanPacketType.Hello, LanBattleProtocol.EncodeHello(localIdentity, localDeck));
                Status = "ホストの応答を待っています…";
            }
            else
            {
                Status = "参加者を確認中…";
            }
        }

        private void PollPeer()
        {
            if (peer == null) return;
            int processed = 0;
            while (processed++ < 64 && peer.TryReceive(out LanPacket packet))
            {
                try
                {
                    switch (packet.Type)
                    {
                        case LanPacketType.Hello when Role == LanSessionRole.Hosting:
                            HandleHello(packet.Payload);
                            break;
                        case LanPacketType.Welcome when Role == LanSessionRole.Joining:
                            Role = LanSessionRole.Guest;
                            Status = "接続完了。ホストの開始を待っています。";
                            nextCommandAt = 0f;
                            break;
                        case LanPacketType.Command when Role == LanSessionRole.Host:
                            remoteInput.Push(LanBattleProtocol.DecodeCommand(packet.Payload));
                            break;
                        case LanPacketType.Snapshot when Role == LanSessionRole.Guest:
                            LanMatchState snapshot = LanBattleProtocol.DecodeSnapshot(packet.Payload);
                            if (snapshot.Sequence > lastAppliedSequence)
                            {
                                lastAppliedSequence = snapshot.Sequence;
                                match.ApplyLanState(snapshot);
                                if (localSettingsOpen) match.SetSettingsPaused(true);
                                Status = (onlineSession ? "ネット対戦中" : "LAN対戦中") +
                                         " / " + match.Score.GamesToWin + "ゲーム先取";
                            }
                            break;
                        case LanPacketType.SettingsPause when Role == LanSessionRole.Host:
                            bool requestedPause = LanBattleProtocol.DecodeSettingsPause(packet.Payload);
                            if (!requestedPause || SettingsAvailabilityPolicy.CanOpen(true, match.HasStarted,
                                    match.Phase, match.ServeRestrictionsActive))
                            {
                                remoteSettingsOpen = requestedPause;
                                RefreshHostSettingsPause();
                            }
                            break;
                        case LanPacketType.Disconnect:
                            Fail("対戦相手が退出しました。");
                            return;
                    }
                }
                catch (Exception exception)
                {
                    Fail("通信データを処理できません: " + exception.Message);
                    return;
                }
            }
        }

        private void HandleHello(byte[] payload)
        {
            if (!LanBattleProtocol.TryDecodeHello(payload, out AthleteIdentity guestIdentity, out CardId[] guestDeck))
            {
                Fail("相手のゲームバージョンまたはデッキが一致しません。");
                return;
            }
            if (!Enum.IsDefined(typeof(AthleteIdentity), guestIdentity) ||
                guestDeck.Any(card => !Enum.IsDefined(typeof(CardId), card) ||
                                      !CardCatalog.IsAllowedFor(card, guestIdentity)))
            {
                Fail("相手のキャラクターまたはデッキに使用できないデータがあります。");
                return;
            }
            farAthlete.SetIdentity(guestIdentity);
            farCards.RebuildDeck(guestDeck);
            match.StartNewMatch(hostedGamesToWin);
            Role = LanSessionRole.Host;
            Status = (onlineSession ? "ネット対戦中" : "LAN対戦中") +
                     " / " + hostedGamesToWin + "ゲーム先取";
            if (!onlineSession) discovery.StopHost();
            peer.Send(LanPacketType.Welcome, Array.Empty<byte>());
            peer.Send(LanPacketType.Snapshot, LanBattleProtocol.EncodeSnapshot(match.CaptureLanState(++snapshotSequence)));
            nextSnapshotAt = Time.unscaledTime + SnapshotInterval;
        }

        private void RefreshHostSettingsPause()
        {
            if (Role == LanSessionRole.Host && match != null)
                match.SetSettingsPaused(localSettingsOpen || remoteSettingsOpen);
        }

        private TennisCommand NormalizeClientCommand(TennisCommand command)
        {
            int directSlot = command.DirectCardSlot;
            if (command.DirectCardPressed && directSlot < 0 && farCards != null)
                directSlot = farCards.ResolveDirectCardSlot(command.CardPointer);
            return new TennisCommand(command.Move, command.Dash, command.StrongPressed, command.SafePressed,
                command.TossPressed, command.SpecialPressed, command.DivePressed, command.DiveDirection,
                command.CardDown, command.CardHeld, command.CardReleased, command.CardPointer,
                command.ShotInputHeld, command.DirectCardPressed, directSlot, command.PreparationChoice);
        }

        private void PrepareAuthoritativeWorld()
        {
            ResolveReferences();
            if (match == null) return;
            match.SetNetworkReplica(false);
            match.SetLocalPlayerSide(CourtSide.Near);
            nearAthlete.SetNetworkReplica(false);
            farAthlete.SetNetworkReplica(false);
            ball.SetNetworkReplica(false);
            nearCards.SetNetworkReplica(false);
            farCards.SetNetworkReplica(false);
            cameraController?.SetLocalSide(CourtSide.Near);
            lastAppliedSequence = 0;
            snapshotSequence = 0;
            localSettingsOpen = false;
            remoteSettingsOpen = false;
            match.SetSettingsPaused(false);
        }

        private void PrepareReplicaWorld()
        {
            ResolveReferences();
            match.ReturnToSetup();
            match.SetLocalPlayerSide(CourtSide.Far);
            match.SetNetworkReplica(true);
            nearAthlete.SetNetworkReplica(true);
            farAthlete.SetNetworkReplica(true);
            ball.SetNetworkReplica(true);
            nearCards.SetNetworkReplica(true);
            farCards.SetNetworkReplica(true);
            cameraController?.SetLocalSide(CourtSide.Far);
            lastAppliedSequence = 0;
            localSettingsOpen = false;
            remoteSettingsOpen = false;
            match.SetSettingsPaused(false);
        }

        private static bool ValidateDeck(IReadOnlyList<CardId> deck) => deck != null && deck.Count == 16;

        private bool Fail(string message)
        {
            StopTransport();
            if (match != null)
            {
                PrepareAuthoritativeWorld();
                nearAthlete.SetCommandSource(localInput);
                farAthlete.SetCommandSource(cpuInput);
                nearCards.SetCpuControlled(false);
                farCards.SetCpuControlled(true);
                cpuInput?.Configure(farAthlete, nearAthlete, ball, match);
                match.ReturnToSetup();
            }
            Role = LanSessionRole.Error;
            ErrorMessage = message;
            Status = message;
            return false;
        }

        private void StopTransport()
        {
            localSettingsOpen = false;
            remoteSettingsOpen = false;
            match?.SetSettingsPaused(false);
            discovery.StopHost();
            try { listener?.Stop(); } catch { }
            listener = null;
            acceptTask = null;
            try { joiningClient?.Close(); } catch { }
            joiningClient = null;
            joinTask = null;
            peer?.Dispose();
            peer = null;
            onlineLobby?.Cancel();
            onlineSession = false;
        }

        private EosOnlineLobbyClient OnlineLobby => onlineLobby ??= new EosOnlineLobbyClient();

        private void OnDestroy()
        {
            StopTransport();
            discovery.Dispose();
            onlineLobby?.Dispose();
            onlineLobby = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && (IsConnected || IsBusy)) Disconnect();
        }
    }
}
