namespace FireAndSteel.Core.Config;

public sealed class RuntimeConfig
{
    public NetworkConfig Network { get; init; } = new();

    public sealed class NetworkConfig
    {
        public string Host { get; init; } = "127.0.0.1";
        public int Port { get; init; } = 0;
    }
}