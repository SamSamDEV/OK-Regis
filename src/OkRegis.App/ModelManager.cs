using System.IO.Compression;
using OkRegis.Core.Languages;
using OkRegis.Core.Localization;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>
/// Trouve (ou télécharge) le modèle vocal Vosk léger de la langue choisie.
/// Dossiers cherchés : "models" à côté de l'exe (l'installateur y met les trois langues), puis %AppData%\OK-Regis\models.
/// </summary>
public static class ModelManager
{
    private static string UserModelsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OK-Regis", "models");

    /// <summary>Chemin du modèle de cette langue s'il est déjà là (et complet), sinon null.</summary>
    public static string? Find(Language language)
    {
        var folder = LanguagePack.For(language).ModelFolder;
        foreach (var root in new[] { Path.Combine(AppContext.BaseDirectory, "models"), UserModelsDir })
        {
            var candidate = Path.Combine(root, folder);
            if (Directory.Exists(candidate) && IsComplete(candidate)) return candidate;
        }
        return null;
    }

    // Un modèle à moitié décompressé n'est pas accepté : il faut le modèle acoustique ET le graphe de mots.
    private static bool IsComplete(string dir) =>
        File.Exists(Path.Combine(dir, "am", "final.mdl")) && File.Exists(Path.Combine(dir, "graph", "HCLr.fst")) && File.Exists(Path.Combine(dir, "graph", "Gr.fst"));

    /// <summary>Télécharge et décompresse le modèle de cette langue (~40 Mo). progress = 0..1.</summary>
    public static async Task<string> DownloadAsync(Language language, IProgress<double> progress)
    {
        var pack = LanguagePack.For(language);
        Directory.CreateDirectory(UserModelsDir);
        var zipPath = Path.Combine(Path.GetTempPath(), pack.ModelFolder + ".zip");
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        using (var response = await http.GetAsync(pack.ModelUrl, HttpCompletionOption.ResponseHeadersRead))
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
        // On décompresse dans un dossier temporaire puis on déplace : une coupure en route ne laisse jamais un modèle à moitié là.
        var temp = Path.Combine(UserModelsDir, "_partiel");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        ZipFile.ExtractToDirectory(zipPath, temp);
        var extracted = Path.Combine(temp, pack.ModelFolder);
        var final = Path.Combine(UserModelsDir, pack.ModelFolder);
        if (Directory.Exists(final)) Directory.Delete(final, true);
        Directory.Move(extracted, final);
        Directory.Delete(temp, true);
        File.Delete(zipPath);
        progress.Report(1);
        return Find(language) ?? throw new InvalidOperationException(Loc.T("Modèle introuvable après décompression."));
    }
}
