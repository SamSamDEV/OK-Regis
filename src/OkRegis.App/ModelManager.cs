using System.IO.Compression;

namespace OkRegis.App;

/// <summary>
/// Trouve (ou télécharge au premier lancement) le modèle Vosk français léger.
/// Dossiers cherchés : "models" à côté de l'exe, puis %AppData%\OK-Regis\models.
/// </summary>
public static class ModelManager
{
    private const string ModelName = "vosk-model-small-fr-0.22";
    private const string Url = "https://alphacephei.com/vosk/models/" + ModelName + ".zip";

    private static string UserModelsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OK-Regis", "models");

    /// <summary>Chemin du modèle s'il est déjà là, sinon null.</summary>
    public static string? Find()
    {
        foreach (var root in new[] { Path.Combine(AppContext.BaseDirectory, "models"), UserModelsDir })
        {
            if (!Directory.Exists(root)) continue;
            var hit = Directory.GetDirectories(root, "vosk-model*").FirstOrDefault(d => File.Exists(WordsFile(d)));
            if (hit != null) return hit;
        }
        return null;
    }

    public static string WordsFile(string modelPath) => Path.Combine(modelPath, "graph", "words.txt");

    /// <summary>Télécharge et décompresse le modèle (~40 Mo). progress = 0..1.</summary>
    public static async Task<string> DownloadAsync(IProgress<double> progress)
    {
        Directory.CreateDirectory(UserModelsDir);
        var zipPath = Path.Combine(Path.GetTempPath(), ModelName + ".zip");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        using (var response = await http.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 42_000_000;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(zipPath);
            var buffer = new byte[81920];
            long done = 0; int n;
            while ((n = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n));
                done += n;
                progress.Report(done / (double)total * 0.9);
            }
        }
        ZipFile.ExtractToDirectory(zipPath, UserModelsDir, overwriteFiles: true);
        File.Delete(zipPath);
        progress.Report(1);
        return Find() ?? throw new InvalidOperationException("Modèle introuvable après décompression.");
    }
}
