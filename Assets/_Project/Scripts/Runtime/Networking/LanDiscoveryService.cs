using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrideCourt.Networking
{
    public sealed class LanDiscoveredHost
    {
        public string Address;
        public int Port;
        public string RoomName;
        public DateTime LastSeenUtc;
        public string Key => Address + ":" + Port;
    }

    public sealed class LanDiscoveryService : IDisposable
    {
        private readonly ConcurrentQueue<LanDiscoveredHost> discovered = new ConcurrentQueue<LanDiscoveredHost>();
        private CancellationTokenSource hostCancellation;
        private UdpClient hostSocket;
        private string roomName = string.Empty;

        public void StartHost(string configuredRoomName)
        {
            StopHost();
            roomName = Sanitize(configuredRoomName);
            hostCancellation = new CancellationTokenSource();
            hostSocket = new UdpClient(AddressFamily.InterNetwork);
            hostSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            hostSocket.Client.Bind(new IPEndPoint(IPAddress.Any, LanBattleProtocol.DiscoveryPort));
            hostSocket.Client.ReceiveTimeout = 500;
            _ = Task.Run(() => HostLoop(hostCancellation.Token));
        }

        public void Scan()
        {
            _ = Task.Run(ScanOnce);
        }

        public bool TryDequeue(out LanDiscoveredHost host) => discovered.TryDequeue(out host);

        public static IReadOnlyList<string> GetLocalAddresses()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                                      adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                      adapter.GetIPProperties().GatewayAddresses.Any(gateway =>
                                          gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                                          !gateway.Address.Equals(IPAddress.Any)))
                    .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                    .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork &&
                                      !IPAddress.IsLoopback(address.Address) &&
                                      !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .Select(address => address.Address.ToString())
                    .Distinct()
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }
        }

        private void HostLoop(CancellationToken token)
        {
            byte[] expected = Encoding.UTF8.GetBytes(LanBattleProtocol.Signature + "|DISCOVER|" + LanBattleProtocol.Version);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] request = hostSocket.Receive(ref endpoint);
                    if (!request.SequenceEqual(expected)) continue;
                    string responseText = LanBattleProtocol.Signature + "|HOST|" + LanBattleProtocol.Version + "|" +
                                          LanBattleProtocol.GamePort + "|" + roomName;
                    byte[] response = Encoding.UTF8.GetBytes(responseText);
                    hostSocket.Send(response, response.Length, endpoint);
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut) { }
                catch (ObjectDisposedException) { return; }
                catch { if (!token.IsCancellationRequested) Thread.Sleep(100); }
            }
        }

        private void ScanOnce()
        {
            try
            {
                using UdpClient socket = new UdpClient(AddressFamily.InterNetwork);
                socket.EnableBroadcast = true;
                socket.Client.ReceiveTimeout = 180;
                byte[] request = Encoding.UTF8.GetBytes(LanBattleProtocol.Signature + "|DISCOVER|" + LanBattleProtocol.Version);
                socket.Send(request, request.Length, new IPEndPoint(IPAddress.Broadcast, LanBattleProtocol.DiscoveryPort));
                DateTime deadline = DateTime.UtcNow.AddSeconds(1.25);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);
                        byte[] response = socket.Receive(ref endpoint);
                        string[] parts = Encoding.UTF8.GetString(response).Split('|');
                        if (parts.Length < 5 || parts[0] != LanBattleProtocol.Signature || parts[1] != "HOST" ||
                            !int.TryParse(parts[2], out int version) || version != LanBattleProtocol.Version ||
                            !int.TryParse(parts[3], out int port)) continue;
                        discovered.Enqueue(new LanDiscoveredHost
                        {
                            Address = endpoint.Address.ToString(), Port = port, RoomName = parts[4], LastSeenUtc = DateTime.UtcNow
                        });
                    }
                    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut) { }
                }
            }
            catch { }
        }

        private static string Sanitize(string value)
        {
            string sanitized = string.IsNullOrWhiteSpace(value) ? "PRIDE COURT" : value.Replace("|", " ").Trim();
            return sanitized.Length <= 32 ? sanitized : sanitized.Substring(0, 32);
        }

        public void StopHost()
        {
            hostCancellation?.Cancel();
            try { hostSocket?.Close(); } catch { }
            hostSocket = null;
            hostCancellation?.Dispose();
            hostCancellation = null;
        }

        public void Dispose()
        {
            StopHost();
        }
    }
}
