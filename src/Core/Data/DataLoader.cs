using System.Text.Json;
using FireAndSteel.Core.Data.Models;

namespace FireAndSteel.Core.Data;

public static class DataLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DataStore LoadAll(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("dataRoot vazio.");

        if (!Directory.Exists(dataRoot))
            throw new DirectoryNotFoundException($"Pasta Data não encontrada: {dataRoot}");

        var manifestPath = Path.Combine(dataRoot, "manifest.json");
        var manifest = ReadJson<DataManifest>(manifestPath);

        var itemsPath = Path.Combine(dataRoot, manifest.Items);
        var mobsPath = Path.Combine(dataRoot, manifest.Mobs);
        var combatPath = Path.Combine(dataRoot, manifest.Combat);
        var dropsPath = Path.Combine(dataRoot, manifest.Drops);

        var itemsDoc = ReadJson<ItemsDoc>(itemsPath);
        var mobsDoc = ReadJson<MobsDoc>(mobsPath);
        var combat = ReadJson<CombatConfig>(combatPath);
        var drops = ReadJson<DropsConfig>(dropsPath);

        var itemsById = itemsDoc.Items.ToDictionary(i => i.Id, i => i);
        var mobsById = mobsDoc.Mobs.ToDictionary(m => m.Id, m => m);

        return new DataStore
        {
            ItemsById = itemsById,
            MobsById = mobsById,
            Combat = combat,
            Drops = drops
        };
    }

    private static T ReadJson<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Arquivo JSON não encontrado.", path);

        var json = File.ReadAllText(path);
        var obj = JsonSerializer.Deserialize<T>(json, JsonOpts);
        if (obj is null)
            throw new InvalidOperationException($"Falha ao desserializar: {path}");

        return obj;
    }
}
