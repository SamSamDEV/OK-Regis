using OkRegis.App.Obs;
using OkRegis.App.Speech;
using OkRegis.Core.Languages;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;
using OkRegis.Core.Localization;

namespace OkRegis.App;

/// <summary>
/// L'appli vit dans la zone de notification : icône, menu, infobulle. La fenêtre principale n'est créée qu'à la demande
/// et détruite quand on la ferme, pour ne rien garder en mémoire quand on ne s'en sert pas.
/// </summary>
public class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly ISecretProtector _secrets;
    private readonly Vocabulary _vocab;
    private readonly Orchestrator _orchestrator;
    private readonly WindowsResponder _responder;
    private readonly ObsProcessWatcher _watcher = new();
    private readonly ObsWebSocketClient _obs = new();
    private readonly VoskMicListener _mic;
    private readonly ResourceMeter _meter = new();
    private readonly NotifyIcon _icon = new();
    private readonly ToolStripMenuItem _pauseItem = new(Loc.T("Mettre l'écoute en pause"));
    private readonly SynchronizationContext _ui;
    private MainForm? _main;   // null tant que la fenêtre n'est pas ouverte
    private DateTime _lastSample = DateTime.MinValue;

    public TrayApp(AppSettings settings, SettingsStore store, ISecretProtector secrets, Vocabulary vocab, ModelHost modelHost)
    {
        // Le contexte WinForms n'existe qu'une fois une fenêtre créée : on s'assure de l'avoir pour revenir sur le thread de l'interface.
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _ui = SynchronizationContext.Current!;
        _settings = settings; _store = store; _secrets = secrets; _vocab = vocab;
        _mic = new VoskMicListener(modelHost, () => _settings.SaveCpuWhenSilent, () => _settings.MicGainDb);
        _responder = new WindowsResponder(settings);
        _orchestrator = new Orchestrator(settings, _obs, _mic, _responder, _watcher, vocab, secrets);

        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.T("Ouvrir la fenêtre"), null, (_, _) => ShowMain());
        _pauseItem.Click += (_, _) => TogglePause();
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("Quitter"), null, (_, _) => Quit());

        _icon.Icon = TrayIcons.For(RegisState.Idle);
        _icon.ContextMenuStrip = menu;
        _icon.Visible = true;
        _icon.DoubleClick += (_, _) => ShowMain();
        _icon.MouseMove += (_, _) => UpdateTooltip(); // on ne mesure qu'au survol, pas en permanence

        // Les événements arrivent de threads de fond : on revient sur le thread de l'interface.
        _orchestrator.Changed += () => _ui.Post(_ => OnChanged(), null);
        _orchestrator.Start();
        UpdateTooltip();
        MemoryTrimmer.Schedule(10_000); // une fois le démarrage terminé, on rend la mémoire inutilisée
    }

    private void OnChanged()
    {
        _icon.Icon = TrayIcons.For(_orchestrator.State);
        _pauseItem.Text = _orchestrator.State == RegisState.Paused ? Loc.T("Reprendre l'écoute") : Loc.T("Mettre l'écoute en pause");
        _main?.RefreshView();
        UpdateTooltip();
        if (_main is not { Visible: true }) MemoryTrimmer.Schedule(8_000); // au repos : on allège (le minuteur est repoussé à chaque activité)
    }

    private void UpdateTooltip()
    {
        if ((DateTime.Now - _lastSample).TotalMilliseconds > 900) { _meter.Sample(); _lastSample = DateTime.Now; }
        var text = $"OK Régis — {OkRegis.App.MainForm.StateText(_orchestrator.State)} — {_meter.Text}";
        _icon.Text = text.Length > 120 ? text[..120] : text;
    }

    private void TogglePause()
    {
        if (_orchestrator.State == RegisState.Paused) _orchestrator.Resume(); else _orchestrator.Pause();
    }

    // Crée la fenêtre si elle n'existe pas, puis l'affiche.
    private void ShowMain()
    {
        if (_main == null)
        {
            _main = new MainForm(_orchestrator, _settings, _vocab, _meter, SaveAndApply, () => _store.Save(_settings),
                (key, text) => _responder.RespondAsync(key, text), ToggleTheme);
            _main.PauseButton.Click += (_, _) => TogglePause();
            _main.SettingsButton.Click += (_, _) => OpenSettings();
            // Fermée par l'utilisateur : on la détruit (un peu plus tard, hors de son propre événement) et on rend la mémoire.
            _main.HiddenToTray += () => _ui.Post(_ =>
            {
                var form = _main; _main = null;
                if (form != null) { form.Exiting = true; form.Dispose(); }
                MemoryTrimmer.Schedule(1_500);
            }, null);
        }
        _main.Show(); _main.WindowState = FormWindowState.Normal; _main.Activate();
        _main.RefreshView();
    }

    private void OpenSettings()
    {
        var oldLanguage = _settings.Language;
        using var form = new SettingsForm(_settings, _secrets, _vocab, firstRun: false);
        if (form.ShowDialog(_main is { Visible: true } ? _main : null) != DialogResult.OK) return;

        // Nouvelle langue : le modèle vocal change, donc l'appli redémarre.
        if (_settings.Language != oldLanguage)
        {
            // Si le nom de l'assistant était celui par défaut, on prend celui de la nouvelle langue.
            if (Normalizer.Normalize(_settings.AssistantName) == Normalizer.Normalize(LanguagePack.For(oldLanguage).DefaultAssistantName))
                _settings.AssistantName = LanguagePack.For(_settings.Language).DefaultAssistantName;
            _store.Save(_settings);
            Autostart.Set(_settings.StartWithWindows);
            Loc.Current = _settings.Language;
            MessageBox.Show(Loc.T("L'appli va redémarrer pour changer de langue."), "OK Régis");
            Program.Restart();
            Quit();
            return;
        }
        _store.Save(_settings);
        Autostart.Set(_settings.StartWithWindows);
        var wasDark = Theme.Dark;
        Theme.Set(_settings.Theme);
        if (Theme.Dark != wasDark) _main?.ReapplyTheme(); // on ne refait tout l'affichage que si le thème a vraiment changé
        _orchestrator.RebuildGrammarInBackground();
    }

    // Bouton ☀/☾ : bascule clair ↔ sombre (et mémorise le choix).
    private void ToggleTheme()
    {
        _settings.Theme = Theme.Dark ? ThemeMode.Light : ThemeMode.Dark;
        _store.Save(_settings);
        Theme.Set(_settings.Theme);
        _main?.ReapplyTheme();
    }

    // Surnom ajouté/retiré : l'affichage suit tout de suite, la grammaire (lourde) se refait en arrière-plan.
    private void SaveAndApply()
    {
        _store.Save(_settings);
        _main?.RefreshView();
        _orchestrator.RebuildGrammarInBackground();
    }

    private void Quit()
    {
        _icon.Visible = false; _icon.Dispose();
        _watcher.Dispose(); _mic.Dispose(); _obs.Dispose();
        if (_main != null) { _main.Exiting = true; _main.Close(); }
        ExitThread();
    }
}
