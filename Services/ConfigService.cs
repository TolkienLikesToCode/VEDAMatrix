using System.Text.Json;
using VedaMatrix.Models;

namespace VedaMatrix.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ProcedureConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", "procedures.json");
        if (!File.Exists(path))
        {
            // Fallback relative
            path = Path.Combine(Directory.GetCurrentDirectory(), "Config", "procedures.json");
        }

        if (!File.Exists(path))
            throw new FileNotFoundException($"Konfigurationsdatei nicht gefunden: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProcedureConfig>(json, Options)
               ?? new ProcedureConfig();
    }

    public void Save(ProcedureConfig config)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", "procedures.json");
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(path, json);
    }
}
