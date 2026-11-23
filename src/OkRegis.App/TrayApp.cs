using OkRegis.App.Obs;
using OkRegis.App.Speech;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>L'appli vit dans la zone de notification : icône, menu, infobulle, fenêtre principale.</summary>
public class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly ISecretProtector _secrets;
    private readonly Vocabulary _vocab;
    private readonly Orchestrator _orchestrator;
    private readonly ObsProcessWatcher _watcher = new();
    private readonly ObsWebSocketClient _obs = new();
    private readonly VoskMicListener _mic;
    private readonly ResourceMeter _meter = new();
    private readonly NotifyIcon _icon = new();
    private readonly ToolStripMenuItem _pauseItem = new("Mettre l'écoute en pause");
    private readonly MainForm _main;
    private readonly SynchronizationContext _ui;
    private DateTime _lastSample = DateTime.MinValue;

    public TrayApp(AppSettings settings, SettingsStore store, ISecretProtector secrets, Vocabulary vocab, ModelHost modelHost)
    {
        // Le contexte WinForms n'existe qu'une fois une fenêtre créée : on s'assure de l'avoir pour revenir sur le thread de l'interface.
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _ui = SynchronizationContext.Current!;
        _settings = settings; _store = store; _secrets = secrets; _vocab = vocab;
        _mic = new VoskMicListener(modelHost);
        _orchestrator = new Orchestrator(settings, _obs, _mic, new WindowsResponder(settings), _watcher, vocab, secrets);
        _main = new MainForm(_orchestrator, settings, vocab, _meter, SaveAndApply);

        _main.PauseButton.Click += (_, _) => TogglePause();
        _main.SettingsButton.Click += (_, _) => OpenSettings();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir la fenêtre", null, (_, _) => ShowMain());
        _pauseItem.Click += (_, _) => TogglePause();
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Quit());

        _icon.Icon = TrayIcons.For(RegisState.Idle);
        _icon.ContextMenuStrip = menu;
        _icon.Visible = true;
        _icon.DoubleClick += (_, _) => ShowMain();
        _icon.MouseMove += (_, _) => UpdateTooltip(); // on ne mesure qu'au survol, pas en permanence

        // Les événements arrivent de threads de fond : on revient sur le thread de l'interface.
        _orchestrator.Changed += () => _ui.Post(_ => OnChanged(), null);
        _orchestrator.Start();
        UpdateTooltip();
    }

    private void OnChanged()
    {
        _icon.Icon = TrayIcons.For(_orchestrator.State);
        _pauseItem.Text = _orchestrator.State == RegisState.Paused ? "Reprendre l'écoute" : "Mettre l'écoute en pause";
        _main.RefreshView();
        UpdateTooltip();
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

    private void ShowMain()
    {
        _main.Show(); _main.WindowState = FormWindowState.Normal; _main.Activate();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _secrets, _vocab, firstRun: false);
        if (form.ShowDialog(_main.Visible ? _main : null) != DialogResult.OK) return;
        SaveAndApply();
    }

    private void SaveAndApply()
    {
        _store.Save(_settings);
        Autostart.Set(_settings.StartWithWindows);
        Theme.Set(_settings.Theme);
        _main.ReapplyTheme();
        _orchestrator.RebuildGrammar(); // changer le nom ou un surnom ne demande que de refaire la grammaire
    }

    private void Quit()
    {
        _icon.Visible = false; _icon.Dispose();
        _watcher.Dispose(); _mic.Dispose(); _obs.Dispose();
        _main.Exiting = true; _main.Close();
        ExitThread();
    }
}
