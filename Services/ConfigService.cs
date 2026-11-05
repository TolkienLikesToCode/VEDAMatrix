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
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Config", "procedures.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Config", "procedures.json"),
            Path.Combine(AppContext.BaseDirectory, "procedures.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "procedures.json"),
            // relative from source tree when running under debugger
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Config", "procedures.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Config", "procedures.json"))
        };

        string? found = null;
        foreach (var path in candidates)
        {
            Console.WriteLine($"[Config] Prüfe: {path}");
            if (File.Exists(path))
            {
                found = path;
                break;
            }
        }

        if (found is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Config] WARNUNG: procedures.json nicht gefunden – verwende leere Konfiguration.");
            Console.ResetColor();
            return new ProcedureConfig();
        }

        Console.WriteLine($"[Config] Geladen: {found}");
        var json = File.ReadAllText(found);
        return JsonSerializer.Deserialize<ProcedureConfig>(json, Options)
               ?? new ProcedureConfig();
    }

    public void Save(ProcedureConfig config)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Config", "procedures.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(path, json);
    }
}
