using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Une seule instance : une deuxième ouvrirait un deuxième micro.
        using var mutex = new Mutex(true, @"Local\OkRegis", out var firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = store.Load();
        var secrets = new DpapiSecretProtector();

        var modelPath = ModelManager.Find() ?? DownloadModel();
        if (modelPath is null) return;
        var vocab = Vocabulary.FromWordsFile(ModelManager.WordsFile(modelPath));

        if (!settings.FirstRunDone)
        {
            using var wizard = new SettingsForm(settings, secrets, vocab, firstRun: true);
            if (wizard.ShowDialog() != DialogResult.OK) return; // pas de configuration → on s'arrête
            store.Save(settings);
            Autostart.Set(settings.StartWithWindows);
        }

        Application.Run(new TrayApp(settings, store, secrets, vocab, modelPath));
    }

    // Premier lancement : télécharge le modèle vocal avec une petite fenêtre de progression.
    private static string? DownloadModel()
    {
        using var form = new Form
        {
            Text = "OK Régis", ClientSize = new Size(380, 80), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, ControlBox = false,
        };
        var label = new Label { Text = "Téléchargement du modèle vocal français (~40 Mo)…", Left = 12, Top = 12, Width = 356 };
        var bar = new ProgressBar { Left = 12, Top = 40, Width = 356, Maximum = 1000 };
        form.Controls.AddRange(new Control[] { label, bar });

        string? result = null;
        form.Shown += async (_, _) =>
        {
            try { result = await ModelManager.DownloadAsync(new Progress<double>(p => bar.Value = (int)(p * 1000))); }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de télécharger le modèle vocal :\n" + ex.Message, "OK Régis");
            }
            form.Close();
        };
        form.ShowDialog();
        return result;
    }
}
