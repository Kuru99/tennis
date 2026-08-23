using System;

namespace PrideCourt.Networking
{
    /// <summary>
    /// Carries the existing battle protocol without exposing LAN or EOS details to match orchestration.
    /// </summary>
    public interface IBattleTransport : IDisposable
    {
        bool IsConnected { get; }
        string LastError { get; }
        void Send(LanPacketType type, byte[] payload);
        bool TryReceive(out LanPacket packet);
    }
}
