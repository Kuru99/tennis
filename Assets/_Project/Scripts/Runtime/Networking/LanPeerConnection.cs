using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PrideCourt.Networking
{
    public sealed class LanPeerConnection : IBattleTransport
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly ConcurrentQueue<byte[]> outgoing = new ConcurrentQueue<byte[]>();
        private readonly ConcurrentQueue<LanPacket> incoming = new ConcurrentQueue<LanPacket>();
        private readonly SemaphoreSlim sendSignal = new SemaphoreSlim(0);
        private readonly Task receiveTask;
        private readonly Task sendTask;
        private int disposed;

        public LanPeerConnection(TcpClient connectedClient)
        {
            client = connectedClient ?? throw new ArgumentNullException(nameof(connectedClient));
            client.NoDelay = true;
            client.ReceiveBufferSize = 128 * 1024;
            client.SendBufferSize = 128 * 1024;
            stream = client.GetStream();
            IsConnected = true;
            receiveTask = Task.Run(ReceiveLoop);
            sendTask = Task.Run(SendLoop);
        }

        public bool IsConnected { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        public void Send(LanPacketType type, byte[] payload)
        {
            if (!IsConnected) return;
            outgoing.Enqueue(LanBattleProtocol.Frame(type, payload));
            sendSignal.Release();
        }

        public bool TryReceive(out LanPacket packet) => incoming.TryDequeue(out packet);

        private async Task ReceiveLoop()
        {
            try
            {
                byte[] lengthBytes = new byte[4];
                while (!cancellation.IsCancellationRequested)
                {
                    await ReadExactly(lengthBytes, 0, lengthBytes.Length, cancellation.Token);
                    int length = BitConverter.ToInt32(lengthBytes, 0);
                    if (length <= 0 || length > LanBattleProtocol.MaximumPacketBytes)
                        throw new InvalidDataException("LAN packet length is invalid.");
                    byte[] frame = new byte[length];
                    await ReadExactly(frame, 0, frame.Length, cancellation.Token);
                    byte[] payload = new byte[length - 1];
                    if (payload.Length > 0) Buffer.BlockCopy(frame, 1, payload, 0, payload.Length);
                    incoming.Enqueue(new LanPacket((LanPacketType)frame[0], payload));
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                LastError = exception.Message;
            }
            finally
            {
                IsConnected = false;
            }
        }

        private async Task SendLoop()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    await sendSignal.WaitAsync(cancellation.Token);
                    while (outgoing.TryDequeue(out byte[] frame))
                    {
                        await stream.WriteAsync(frame, 0, frame.Length, cancellation.Token);
                    }
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                LastError = exception.Message;
            }
            finally
            {
                IsConnected = false;
            }
        }

        private async Task ReadExactly(byte[] buffer, int offset, int count, CancellationToken token)
        {
            int read = 0;
            while (read < count)
            {
                int chunk = await stream.ReadAsync(buffer, offset + read, count - read, token);
                if (chunk <= 0) throw new EndOfStreamException("LAN peer closed the connection.");
                read += chunk;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            IsConnected = false;
            cancellation.Cancel();
            try { client.Close(); } catch { }
            try { sendSignal.Release(); } catch { }
            try { Task.WaitAll(new[] { receiveTask, sendTask }, 750); } catch { }
            try { sendSignal.Dispose(); } catch { }
            try { cancellation.Dispose(); } catch { }
        }
    }
}
