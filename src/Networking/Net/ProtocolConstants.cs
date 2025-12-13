namespace FireAndSteel.Networking.Net;

public static class ProtocolConstants
{
    public const ushort ProtocolVersion = 1;

    // Limites defensivos (rede, não “balanceamento”)
    public const int MaxBodyBytes = 1024 * 1024; // 1MB
}
