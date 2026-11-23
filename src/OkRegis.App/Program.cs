using System.Diagnostics;
using OkRegis.App.Speech;
using OkRegis.Core.Languages;
using OkRegis.Core.Localization;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Une seule instance : une deuxième ouvrirait un deuxième micro.
        // Après un changement de langue ("--restart"), la nouvelle instance attend que l'ancienne se termine.
        var restarting = args.Contains("--restart");
        using var mutex = new Mutex(false, @"Local\OkRegis");
        bool owned;
        try { owned = mutex.WaitOne(restarting ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { owned = true; } // l'ancienne instance s'est arrêtée sans libérer : on prend la main
        if (!owned) return;
        try { Run(); }
        finally { mutex.ReleaseMutex(); }
    }

    private static void Run()
    {
        ApplicationConfiguration.Initialize();
        var store = new SettingsStore(SettingsStore.DefaultPath);
        var settings = store.Load();
        var secrets = new DpapiSecretProtector();
        Theme.Set(settings.Theme);

        // Tout premier lancement : on commence par choisir la langue.
        if (!settings.FirstRunDone)
        {
            var chosen = LanguagePicker.Ask();
            if (chosen is null) return;
            settings.Language = chosen.Value;
            settings.AssistantName = LanguagePack.For(chosen.Value).DefaultAssistantName;
        }
        Loc.Current = settings.Language;

        var modelPath = ModelManager.Find(settings.Language) ?? DownloadModel(settings.Language);
        if (modelPath is null) return;
        using var modelHost = new ModelHost(modelPath);
        var vocab = new Vocabulary(modelHost.WordExists, settings.Language); // le modèle léger n'a pas de liste de mots : on l'interroge

        if (!settings.FirstRunDone)
        {
            using var wizard = new SettingsForm(settings, secrets, vocab, firstRun: true);
            if (wizard.ShowDialog() != DialogResult.OK) return; // pas de configuration → on s'arrête
            store.Save(settings);
            Autostart.Set(settings.StartWithWindows);
        }

        Application.Run(new TrayApp(settings, store, secrets, vocab, modelHost));
    }

    /// <summary>Relance l'appli (changement de langue) : la nouvelle instance attend que celle-ci soit terminée.</summary>
    public static void Restart()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart") { UseShellExecute = true });
    }

    // Télécharge le modèle vocal de la langue avec une petite fenêtre de progression.
    private static string? DownloadModel(Language language)
    {
        using var form = new Form
        {
            Text = "OK Régis", ClientSize = new Size(420, 90), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, ControlBox = false,
        };
        var label = new Label { Text = Loc.T("Téléchargement du modèle vocal ({0}, ~40 Mo)…", Loc.NameOf(language)), Left = 14, Top = 14, Width = 392 };
        var bar = new ProgressBar { Left = 14, Top = 44, Width = 392, Maximum = 1000 };
        form.Controls.AddRange(new Control[] { label, bar });
        Theme.Apply(form);

        string? result = null;
        form.Shown += async (_, _) =>
        {
            try { result = await ModelManager.DownloadAsync(language, new Progress<double>(p => bar.Value = (int)(p * 1000))); }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.T("Impossible de télécharger le modèle vocal :\n") + ex.Message, "OK Régis");
            }
            form.Close();
        };
        form.ShowDialog();
        return result;
    }
}
