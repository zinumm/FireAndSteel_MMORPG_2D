using System.Text.Json;

namespace FireAndSteel.Core.Config;

public static class JsonConfig
{
    public static RuntimeConfig LoadRuntime(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Config path vazio.");
        
        if (!File.Exists(path))
            throw new FileNotFoundException("Arquivo de config não encontrado.", path);
        
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<RuntimeConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cfg is null)
            throw new InvalidOperationException("Falha ao desserializar runtime.json");

        if (string.IsNullOrWhiteSpace(cfg.Network.Host))
            throw new InvalidOperationException("networl.host inválido");

        if (cfg.Network.Port < 1 || cfg.Network.Port > 65535)
            throw new InvalidOperationException("network.port fora do range (1..65535).");

        return cfg;
    }
}