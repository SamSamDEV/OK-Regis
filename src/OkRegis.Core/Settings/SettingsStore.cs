using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OkRegis.Core.Settings;

/// <summary>Lit et écrit les réglages dans un fichier JSON.</summary>
public class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // garde les accents lisibles
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    public SettingsStore(string path) => _path = path;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OK-Regis", "settings.json");

    /// <summary>Charge les réglages. Fichier absent ou abîmé → réglages par défaut.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".bad", overwrite: true); // on garde le fichier abîmé
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
    }
}
