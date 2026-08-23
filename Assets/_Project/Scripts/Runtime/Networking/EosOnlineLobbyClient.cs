using System;
#if PRIDE_COURT_EOS
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;
using PlayEveryWare.EpicOnlineServices.Utility;
using UnityEngine;
#endif

namespace PrideCourt.Networking
{
    public enum EosOnlineLobbyState
    {
        Idle,
        Unconfigured,
        Authenticating,
        Creating,
        Searching,
        Joining,
        WaitingForOpponent,
        Ready,
        Error
    }

#if PRIDE_COURT_EOS
    /// <summary>
    /// Owns EOS Connect authentication and two-player lobby discovery. Match packets remain in IBattleTransport.
    /// </summary>
    public sealed class EosOnlineLobbyClient : IDisposable
    {
        private const string BucketId = "PRIDE_COURT_PROTOCOL_2";
        private const string RoomCodeAttribute = "PRIDE_ROOM_CODE";

        private LobbyInterface lobby;
        private ProductUserId localUserId;
        private ProductUserId remoteUserId;
        private LobbyDetails pendingLobbyDetails;
        private string lobbyId = string.Empty;
        private bool isHost;
        private bool transportTaken;
        private ulong memberStatusNotification;
        private int operationVersion;

        public EosOnlineLobbyState State { get; private set; } = EosOnlineLobbyState.Idle;
        public string Status { get; private set; } = "EOS設定を確認してください。";
        public string ErrorMessage { get; private set; } = string.Empty;
        public string RoomCode { get; private set; } = string.Empty;
        public bool IsConfigured => TryGetConfigurationError(out _);
        public bool IsBusy => State == EosOnlineLobbyState.Authenticating || State == EosOnlineLobbyState.Creating ||
                              State == EosOnlineLobbyState.Searching || State == EosOnlineLobbyState.Joining;
        public bool IsInLobby => State == EosOnlineLobbyState.WaitingForOpponent || State == EosOnlineLobbyState.Ready;
        public bool IsHost => isHost;

        public string ConfigurationStatus
        {
            get
            {
                if (TryGetConfigurationError(out string error)) return "EOS設定済み";
                return error;
            }
        }

        public bool Host()
        {
            Cancel();
            isHost = true;
            RoomCode = EosRoomCode.Create();
            int version = operationVersion;
            return Authenticate(version, () => CreateLobby(version));
        }

        public bool Join(string roomCode)
        {
            string normalized = EosRoomCode.Normalize(roomCode);
            if (!EosRoomCode.IsValid(normalized)) return Fail("6桁のコートコードを入力してください。");
            Cancel();
            isHost = false;
            RoomCode = normalized;
            int version = operationVersion;
            return Authenticate(version, () => SearchLobby(version));
        }

        public bool TryTakeTransport(out IBattleTransport transport)
        {
            transport = null;
            if (transportTaken || State != EosOnlineLobbyState.Ready || remoteUserId == null || !remoteUserId.IsValid())
                return false;
            transport = new EosP2PTransport(remoteUserId);
            if (!transport.IsConnected)
            {
                string error = transport.LastError;
                transport.Dispose();
                return Fail(error);
            }
            transportTaken = true;
            return true;
        }

        private bool Authenticate(int version, Action onReady)
        {
            if (!TryGetConfigurationError(out string configurationError)) return Fail(configurationError, true);
            try
            {
                EnsureManager();
                if (EOSManager.Instance.GetEOSPlatformInterface() == null)
                    return Fail("EOSを初期化できません。設定内容を確認してください。");
                lobby = EOSManager.Instance.GetEOSLobbyInterface();
                localUserId = EOSManager.Instance.GetProductUserId();
                if (localUserId != null && localUserId.IsValid())
                {
                    SubscribeMemberStatus();
                    onReady();
                    return true;
                }

                State = EosOnlineLobbyState.Authenticating;
                Status = "EOSへ匿名接続中…";
                ConnectInterface connect = EOSManager.Instance.GetEOSConnectInterface();
                CreateDeviceIdOptions options = new CreateDeviceIdOptions
                {
                    DeviceModel = TrimTo(SystemInfo.deviceModel, 64, Application.platform.ToString())
                };
                connect.CreateDeviceId(ref options, null, (ref CreateDeviceIdCallbackInfo data) =>
                {
                    if (version != operationVersion) return;
                    if (data.ResultCode != Result.Success && data.ResultCode != Result.DuplicateNotAllowed)
                    {
                        Fail("EOS端末認証エラー: " + data.ResultCode);
                        return;
                    }
                    LoginWithDeviceId(version, onReady);
                });
                return true;
            }
            catch (Exception exception)
            {
                return Fail("EOS初期化エラー: " + exception.Message);
            }
        }

        private void LoginWithDeviceId(int version, Action onReady)
        {
            string displayName = TrimTo(SystemInfo.deviceName, 32, "PRIDE PLAYER");
            EOSManager.Instance.StartConnectLoginWithDeviceToken(displayName, data =>
            {
                if (version != operationVersion) return;
                if (data.ResultCode == Result.Success && data.LocalUserId != null)
                {
                    CompleteAuthentication(onReady);
                    return;
                }
                if (data.ResultCode != Result.InvalidUser || data.ContinuanceToken == null)
                {
                    Fail("EOSログインエラー: " + data.ResultCode);
                    return;
                }
                EOSManager.Instance.CreateConnectUserWithContinuanceToken(data.ContinuanceToken, created =>
                {
                    if (version != operationVersion) return;
                    if (created.ResultCode != Result.Success)
                    {
                        Fail("EOSユーザー作成エラー: " + created.ResultCode);
                        return;
                    }
                    CompleteAuthentication(onReady);
                });
            });
        }

        private void CompleteAuthentication(Action onReady)
        {
            localUserId = EOSManager.Instance.GetProductUserId();
            if (localUserId == null || !localUserId.IsValid())
            {
                Fail("EOSユーザーIDを取得できません。");
                return;
            }
            lobby = EOSManager.Instance.GetEOSLobbyInterface();
            SubscribeMemberStatus();
            onReady();
        }

        private void CreateLobby(int version)
        {
            State = EosOnlineLobbyState.Creating;
            Status = "オンラインコートを開設中…";
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                LocalUserId = localUserId,
                MaxLobbyMembers = 2,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                PresenceEnabled = false,
                AllowInvites = false,
                BucketId = BucketId,
                DisableHostMigration = true,
                EnableRTCRoom = false,
                EnableJoinById = false,
                RejoinAfterKickRequiresInvite = false
            };
            lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo data) =>
            {
                if (version != operationVersion)
                {
                    if (data.ResultCode == Result.Success) DestroyStaleLobby(data.LobbyId.ToString());
                    return;
                }
                if (data.ResultCode != Result.Success)
                {
                    Fail("EOSロビー作成エラー: " + data.ResultCode);
                    return;
                }
                lobbyId = data.LobbyId.ToString();
                PublishRoomCode(version);
            });
        }

        private void PublishRoomCode(int version)
        {
            UpdateLobbyModificationOptions modificationOptions = new UpdateLobbyModificationOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId
            };
            Result result = lobby.UpdateLobbyModification(ref modificationOptions, out LobbyModification modification);
            if (result != Result.Success)
            {
                Fail("コートコード準備エラー: " + result);
                return;
            }
            AttributeData data = new AttributeData { Key = RoomCodeAttribute, Value = RoomCode };
            LobbyModificationAddAttributeOptions attributeOptions = new LobbyModificationAddAttributeOptions
            {
                Attribute = data,
                Visibility = LobbyAttributeVisibility.Public
            };
            result = modification.AddAttribute(ref attributeOptions);
            if (result != Result.Success)
            {
                modification.Release();
                Fail("コートコード登録エラー: " + result);
                return;
            }
            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobby.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo callback) =>
            {
                if (version != operationVersion) return;
                if (callback.ResultCode != Result.Success)
                {
                    Fail("コートコード公開エラー: " + callback.ResultCode);
                    return;
                }
                State = EosOnlineLobbyState.WaitingForOpponent;
                Status = "コートコード " + RoomCode + " / 相手を待っています。";
            });
            modification.Release();
        }

        private void SearchLobby(int version)
        {
            State = EosOnlineLobbyState.Searching;
            Status = "コートコード " + RoomCode + " を検索中…";
            CreateLobbySearchOptions createOptions = new CreateLobbySearchOptions { MaxResults = 4 };
            Result result = lobby.CreateLobbySearch(ref createOptions, out LobbySearch search);
            if (result != Result.Success)
            {
                Fail("EOSロビー検索準備エラー: " + result);
                return;
            }
            LobbySearchSetParameterOptions parameterOptions = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = new AttributeData { Key = RoomCodeAttribute, Value = RoomCode }
            };
            result = search.SetParameter(ref parameterOptions);
            if (result != Result.Success)
            {
                search.Release();
                Fail("コートコード検索エラー: " + result);
                return;
            }
            LobbySearchFindOptions findOptions = new LobbySearchFindOptions { LocalUserId = localUserId };
            search.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo callback) =>
            {
                if (version != operationVersion)
                {
                    search.Release();
                    return;
                }
                if (callback.ResultCode != Result.Success)
                {
                    search.Release();
                    Fail(callback.ResultCode == Result.NotFound
                        ? "そのコートコードは見つかりません。"
                        : "EOSロビー検索エラー: " + callback.ResultCode);
                    return;
                }
                LobbySearchGetSearchResultCountOptions countOptions = new LobbySearchGetSearchResultCountOptions();
                uint count = search.GetSearchResultCount(ref countOptions);
                if (count == 0)
                {
                    search.Release();
                    Fail("そのコートコードは見つかりません。");
                    return;
                }
                JoinFirstCompatibleLobby(search, count, version);
                search.Release();
            });
        }

        private void JoinFirstCompatibleLobby(LobbySearch search, uint count, int version)
        {
            for (uint index = 0; index < count; index++)
            {
                LobbySearchCopySearchResultByIndexOptions copyOptions =
                    new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = index };
                if (search.CopySearchResultByIndex(ref copyOptions, out LobbyDetails details) != Result.Success ||
                    details == null) continue;
                LobbyDetailsCopyInfoOptions infoOptions = new LobbyDetailsCopyInfoOptions();
                Result infoResult = details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info);
                if (infoResult != Result.Success || !info.HasValue || info.Value.BucketId.ToString() != BucketId ||
                    info.Value.LobbyOwnerUserId == null || !info.Value.LobbyOwnerUserId.IsValid())
                {
                    details.Release();
                    continue;
                }
                remoteUserId = info.Value.LobbyOwnerUserId;
                pendingLobbyDetails = details;
                JoinLobby(version);
                return;
            }
            Fail("同じゲーム版のコートが見つかりません。");
        }

        private void JoinLobby(int version)
        {
            State = EosOnlineLobbyState.Joining;
            Status = "オンラインコートへ参加中…";
            JoinLobbyOptions options = new JoinLobbyOptions
            {
                LobbyDetailsHandle = pendingLobbyDetails,
                LocalUserId = localUserId,
                PresenceEnabled = false
            };
            lobby.JoinLobby(ref options, null, (ref JoinLobbyCallbackInfo data) =>
            {
                pendingLobbyDetails?.Release();
                pendingLobbyDetails = null;
                if (version != operationVersion)
                {
                    if (data.ResultCode == Result.Success) LeaveStaleLobby(data.LobbyId.ToString());
                    return;
                }
                if (data.ResultCode != Result.Success)
                {
                    Fail("EOSロビー参加エラー: " + data.ResultCode);
                    return;
                }
                lobbyId = data.LobbyId.ToString();
                State = EosOnlineLobbyState.Ready;
                Status = "対戦相手へ接続中…";
            });
        }

        private void SubscribeMemberStatus()
        {
            if (lobby == null || memberStatusNotification != 0) return;
            AddNotifyLobbyMemberStatusReceivedOptions options = new AddNotifyLobbyMemberStatusReceivedOptions();
            memberStatusNotification = lobby.AddNotifyLobbyMemberStatusReceived(ref options, null,
                (ref LobbyMemberStatusReceivedCallbackInfo data) =>
                {
                    if (string.IsNullOrEmpty(lobbyId) || data.LobbyId.ToString() != lobbyId ||
                        data.TargetUserId == null || data.TargetUserId.Equals(localUserId)) return;
                    if (data.CurrentStatus == LobbyMemberStatus.Joined && isHost)
                    {
                        remoteUserId = data.TargetUserId;
                        State = EosOnlineLobbyState.Ready;
                        Status = "対戦相手へ接続中…";
                    }
                    else if (data.CurrentStatus == LobbyMemberStatus.Left ||
                             data.CurrentStatus == LobbyMemberStatus.Disconnected ||
                             data.CurrentStatus == LobbyMemberStatus.Kicked ||
                             data.CurrentStatus == LobbyMemberStatus.Closed)
                    {
                        Fail("対戦相手がオンラインコートから退出しました。");
                    }
                });
        }

        public void Cancel()
        {
            operationVersion++;
            transportTaken = false;
            remoteUserId = null;
            pendingLobbyDetails?.Release();
            pendingLobbyDetails = null;
            if (lobby != null && localUserId != null && localUserId.IsValid() && !string.IsNullOrEmpty(lobbyId))
            {
                if (isHost)
                {
                    DestroyLobbyOptions options = new DestroyLobbyOptions { LocalUserId = localUserId, LobbyId = lobbyId };
                    lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo _) => { });
                }
                else
                {
                    LeaveLobbyOptions options = new LeaveLobbyOptions { LocalUserId = localUserId, LobbyId = lobbyId };
                    lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo _) => { });
                }
            }
            lobbyId = string.Empty;
            RoomCode = string.Empty;
            ErrorMessage = string.Empty;
            State = EosOnlineLobbyState.Idle;
            Status = "EOS設定を確認してください。";
        }

        private bool Fail(string message, bool unconfigured = false)
        {
            ErrorMessage = message;
            Status = message;
            State = unconfigured ? EosOnlineLobbyState.Unconfigured : EosOnlineLobbyState.Error;
            return false;
        }

        private void DestroyStaleLobby(string staleLobbyId)
        {
            if (lobby == null || localUserId == null || string.IsNullOrEmpty(staleLobbyId)) return;
            DestroyLobbyOptions options = new DestroyLobbyOptions
                { LocalUserId = localUserId, LobbyId = staleLobbyId };
            lobby.DestroyLobby(ref options, null, (ref DestroyLobbyCallbackInfo _) => { });
        }

        private void LeaveStaleLobby(string staleLobbyId)
        {
            if (lobby == null || localUserId == null || string.IsNullOrEmpty(staleLobbyId)) return;
            LeaveLobbyOptions options = new LeaveLobbyOptions
                { LocalUserId = localUserId, LobbyId = staleLobbyId };
            lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo _) => { });
        }

        private static bool TryGetConfigurationError(out string error)
        {
            try
            {
                string productConfigPath = FileSystemUtility.CombinePaths(
                    Application.streamingAssetsPath, "EOS", "eos_product_config.json");
                if (!FileSystemUtility.FileExists(productConfigPath))
                {
                    error = "EOS接続情報が未登録です。下の「EOS設定を開く」から登録してください。";
                    return false;
                }
                ProductConfig product = Config.Get<ProductConfig>();
                if (product == null || product.ProductId == Guid.Empty || string.IsNullOrWhiteSpace(product.ProductName) ||
                    string.IsNullOrWhiteSpace(product.ProductVersion) ||
                    !product.TryGetFirstCompleteNamedClientCredentials(out _) ||
                    !product.Environments.TryGetFirstDefinedNamedDeployment(out _))
                {
                    error = "EOS設定未完了: EOS Plugin > EOS Configuration を設定してください。";
                    return false;
                }
                PlatformConfig platform = PlatformManager.GetPlatformConfig();
                if (platform == null || !platform.deployment.IsComplete ||
                    platform.clientCredentials is not { IsComplete: true })
                {
                    error = "EOS設定未完了: この端末向けのDeploymentとClient Credentialsを選んでください。";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch
            {
                error = "EOS接続情報を読み込めません。UnityでEOS Configurationを確認してください。";
                return false;
            }
        }

        private static void EnsureManager()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EOSManager>() != null) return;
            GameObject managerObject = new GameObject("Epic Online Services");
            managerObject.AddComponent<EOSManager>();
        }

        private static string TrimTo(string value, int maximum, string fallback)
        {
            string resolved = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return resolved.Length <= maximum ? resolved : resolved.Substring(0, maximum);
        }

        public void Dispose()
        {
            Cancel();
            if (lobby != null && memberStatusNotification != 0)
            {
                lobby.RemoveNotifyLobbyMemberStatusReceived(memberStatusNotification);
                memberStatusNotification = 0;
            }
        }
    }
#else
    /// <summary>
    /// Keeps the online boundary stable while the optional EOS integration is not included in public builds.
    /// </summary>
    public sealed class EosOnlineLobbyClient : IDisposable
    {
        public EosOnlineLobbyState State => EosOnlineLobbyState.Unconfigured;
        public string Status => "ネット対戦は※実装前です。";
        public string ErrorMessage => string.Empty;
        public string RoomCode => string.Empty;
        public bool IsConfigured => false;
        public bool IsBusy => false;
        public bool IsInLobby => false;
        public bool IsHost => false;
        public string ConfigurationStatus => Status;

        public bool Host() => false;
        public bool Join(string roomCode) => false;

        public bool TryTakeTransport(out IBattleTransport transport)
        {
            transport = null;
            return false;
        }

        public void Cancel() { }
        public void Dispose() { }
    }
#endif
}
