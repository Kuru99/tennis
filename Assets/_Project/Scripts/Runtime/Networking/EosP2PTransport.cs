#if PRIDE_COURT_EOS
using System;
using System.Collections.Concurrent;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;

namespace PrideCourt.Networking
{
    public sealed class EosP2PTransport : IBattleTransport
    {
        public const int MaximumPacketSize = P2PInterface.MAX_PACKET_SIZE;

        private const byte BattleChannel = 0;
        private const string SocketName = "PRIDECOURT";

        private readonly ConcurrentQueue<LanPacket> incoming = new ConcurrentQueue<LanPacket>();
        private readonly P2PInterface p2p;
        private readonly ProductUserId localUserId;
        private readonly ProductUserId remoteUserId;
        private readonly SocketId socketId = new SocketId { SocketName = SocketName };
        private ulong connectionRequestNotification;
        private bool disposed;

        public EosP2PTransport(ProductUserId remoteUser)
        {
            localUserId = EOSManager.Instance.GetProductUserId();
            remoteUserId = remoteUser;
            p2p = EOSManager.Instance.GetEOSP2PInterface();
            IsConnected = p2p != null && localUserId != null && localUserId.IsValid() &&
                          remoteUserId != null && remoteUserId.IsValid();
            if (!IsConnected)
            {
                LastError = "EOS P2Pを開始できません。";
                return;
            }

            AddNotifyPeerConnectionRequestOptions options = new AddNotifyPeerConnectionRequestOptions
            {
                LocalUserId = localUserId,
                SocketId = socketId
            };
            connectionRequestNotification = p2p.AddNotifyPeerConnectionRequest(
                ref options, null, OnIncomingConnectionRequest);
        }

        public bool IsConnected { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public void Send(LanPacketType type, byte[] payload)
        {
            if (!IsConnected) return;
            byte[] frame = LanBattleProtocol.Frame(type, payload);
            if (frame.Length > P2PInterface.MAX_PACKET_SIZE)
            {
                Fail("EOS送信データが上限を超えました。");
                return;
            }

            SendPacketOptions options = new SendPacketOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId,
                Channel = BattleChannel,
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableOrdered,
                Data = new ArraySegment<byte>(frame)
            };
            Result result = p2p.SendPacket(ref options);
            if (result != Result.Success) Fail("EOS送信エラー: " + result);
        }

        public bool TryReceive(out LanPacket packet)
        {
            PumpIncoming();
            return incoming.TryDequeue(out packet);
        }

        private void PumpIncoming()
        {
            if (!IsConnected) return;
            int processed = 0;
            while (processed++ < 64)
            {
                GetNextReceivedPacketSizeOptions sizeOptions = new GetNextReceivedPacketSizeOptions
                {
                    LocalUserId = localUserId,
                    RequestedChannel = BattleChannel
                };
                Result sizeResult = p2p.GetNextReceivedPacketSize(ref sizeOptions, out uint size);
                if (sizeResult == Result.NotFound || size == 0) return;
                if (sizeResult != Result.Success)
                {
                    Fail("EOS受信確認エラー: " + sizeResult);
                    return;
                }
                if (size > P2PInterface.MAX_PACKET_SIZE)
                {
                    Fail("EOS受信データが上限を超えました。");
                    return;
                }

                byte[] buffer = new byte[size];
                ProductUserId sender = null;
                SocketId receivedSocket = default;
                ReceivePacketOptions receiveOptions = new ReceivePacketOptions
                {
                    LocalUserId = localUserId,
                    MaxDataSizeBytes = size,
                    RequestedChannel = BattleChannel
                };
                Result receiveResult = p2p.ReceivePacket(ref receiveOptions, ref sender, ref receivedSocket,
                    out _, new ArraySegment<byte>(buffer), out uint bytesWritten);
                if (receiveResult == Result.NotFound) return;
                if (receiveResult != Result.Success)
                {
                    Fail("EOS受信エラー: " + receiveResult);
                    return;
                }
                if (sender == null || !sender.Equals(remoteUserId) || bytesWritten != size) continue;
                if (!LanBattleProtocol.TryParseFrame(buffer, out LanPacket decoded))
                {
                    Fail("EOS受信データの形式が不正です。");
                    return;
                }
                incoming.Enqueue(decoded);
            }
        }

        private void OnIncomingConnectionRequest(ref OnIncomingConnectionRequestInfo data)
        {
            if (!IsConnected || data.RemoteUserId == null || !data.RemoteUserId.Equals(remoteUserId)) return;
            if (data.SocketId == null || data.SocketId.Value.SocketName != SocketName) return;
            AcceptConnectionOptions options = new AcceptConnectionOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId
            };
            Result result = p2p.AcceptConnection(ref options);
            if (result != Result.Success) Fail("EOS接続受付エラー: " + result);
        }

        private void Fail(string message)
        {
            LastError = message;
            IsConnected = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsConnected = false;
            if (p2p == null) return;
            if (connectionRequestNotification != 0)
            {
                p2p.RemoveNotifyPeerConnectionRequest(connectionRequestNotification);
                connectionRequestNotification = 0;
            }
            if (localUserId == null || remoteUserId == null) return;
            CloseConnectionOptions options = new CloseConnectionOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = remoteUserId,
                SocketId = socketId
            };
            p2p.CloseConnection(ref options);
        }
    }
}
#else
namespace PrideCourt.Networking
{
    public static class EosP2PTransport
    {
        public const int MaximumPacketSize = 1170;
    }
}
#endif
