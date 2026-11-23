using OkRegis.Core.Commands;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;
using OkRegis.Core.Localization;

namespace OkRegis.App;

/// <summary>Fenêtre principale : état, scènes et surnoms (une carte par nom), réponses de Régis, historique.</summary>
public class MainForm : Form
{
    private readonly Orchestrator _o;
    private readonly AppSettings _settings;
    private readonly Vocabulary _vocab;
    private readonly ResourceMeter _meter;
    private readonly Action _nicknamesChanged; // enregistre et reconstruit la grammaire
    private readonly Action _save;             // enregistre seulement
    private readonly Func<string, string, Task> _preview;
    private readonly Action _toggleTheme;

    private readonly Label _dot = new() { Text = "●", AutoSize = true, Font = new Font("Segoe UI", 16f) };
    private readonly Label _state = new() { AutoSize = true };
    private readonly Label _lastHeard = new() { AutoSize = true, Tag = "muted" };
    private readonly Button _themeButton = new() { Width = 46, Height = 36, Font = new Font("Segoe UI Symbol", 13f) };
    private readonly ToolTip _tip = new();
    private readonly ScenesView _scenes;
    private readonly ScenesView _sounds;
    private readonly ResponsesView _responses;
    private readonly ListView _history = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    private readonly Label _resources = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", Padding = new Padding(16, 0, 0, 0) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 }; // actif seulement quand la fenêtre est visible

    public bool Exiting { get; set; }

    /// <summary>Levé quand l'utilisateur ferme la fenêtre : l'appli la détruit pour libérer la mémoire.</summary>
    public event Action? HiddenToTray;
    public Button PauseButton { get; } = new() { Text = Loc.T("Mettre en pause"), AutoSize = true };
    public Button SettingsButton { get; } = new() { Text = Loc.T("Réglages"), AutoSize = true };
    private readonly Button _refreshButton = new() { Text = Loc.T("↻  Actualiser"), AutoSize = true };

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public MainForm(Orchestrator orchestrator, AppSettings settings, Vocabulary vocab, ResourceMeter meter,
        Action nicknamesChanged, Action save, Func<string, string, Task> preview, Action toggleTheme)
    {
        _o = orchestrator; _settings = settings; _vocab = vocab; _meter = meter;
        _nicknamesChanged = nicknamesChanged; _save = save; _preview = preview; _toggleTheme = toggleTheme;
        _scenes = new ScenesView(orchestrator, settings, vocab, nicknamesChanged, save) { Dock = DockStyle.Fill };
        _sounds = new ScenesView(orchestrator, settings, vocab, nicknamesChanged, save, audioOnly: true) { Dock = DockStyle.Fill };
        _responses = new ResponsesView(settings, save, preview) { Dock = DockStyle.Fill };
        Text = "OK Régis"; ClientSize = new Size(840, 620); MinimumSize = new Size(720, 520); StartPosition = FormStartPosition.CenterScreen;
        Icon = (Icon)TrayIcons.For(RegisState.Listening).Clone(); // une copie : détruire la fenêtre ne doit pas détruire l'icône partagée

        _history.Columns.Add(Loc.T("Heure"), 80); _history.Columns.Add(Loc.T("Entendu"), 300); _history.Columns.Add(Loc.T("Résultat"), 380);
        _themeButton.Click += (_, _) => _toggleTheme();
        _refreshButton.Click += (_, _) => { _scenes.ForceRebuild(); _sounds.ForceRebuild(); _o.Refresh(); RefreshView(); };

        var tabs = new NavTabs();
        tabs.Add(Loc.T("Scènes"), _scenes);
        tabs.Add(Loc.T("Sons"), _sounds);
        tabs.Add(Loc.T("Réponses de Régis"), _responses);
        tabs.Add(Loc.T("Historique"), _history);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        footer.Controls.Add(_resources);
        Controls.Add(tabs); Controls.Add(BuildHeader()); Controls.Add(footer);

        Theme.Apply(this);
        UpdateThemeButton();
        ApplyStateColors();

        _timer.Tick += (_, _) => UpdateResources();
        // Le minuteur ne tourne que si la fenêtre est affichée : zéro travail quand elle est cachée ou réduite.
        VisibleChanged += (_, _) => UpdateTimer();
        Resize += (_, _) => UpdateTimer();
        FormClosing += (_, e) => { if (!Exiting) { e.Cancel = true; Hide(); HiddenToTray?.Invoke(); } };
    }

    private void UpdateTimer()
    {
        var active = Visible && WindowState != FormWindowState.Minimized;
        _timer.Enabled = active;
        if (active) { RefreshView(); UpdateResources(); }
    }

    // ---- en-tête ----

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 92, Tag = "card", Padding = new Padding(22, 14, 22, 10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 440, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0), Tag = "card" };
        buttons.Controls.Add(_themeButton); buttons.Controls.Add(SettingsButton); buttons.Controls.Add(_refreshButton); buttons.Controls.Add(PauseButton);
        _state.Font = Theme.TitleFont;
        var stateRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Tag = "card" };
        stateRow.Controls.Add(_dot); stateRow.Controls.Add(_state);
        _lastHeard.Dock = DockStyle.Top; _lastHeard.Padding = new Padding(4, 0, 0, 0);
        header.Controls.Add(_lastHeard); header.Controls.Add(stateRow); header.Controls.Add(buttons);
        return header;
    }

    public static string StateText(RegisState s) => s switch
    {
        RegisState.Idle => Loc.T("En veille (OBS fermé)"),
        RegisState.Connecting => Loc.T("Connexion à OBS…"),
        RegisState.Listening => Loc.T("À l'écoute"),
        _ => Loc.T("En pause"),
    };

    private void ApplyStateColors() => _dot.ForeColor = _o.State switch
    {
        RegisState.Listening => Theme.Success,
        RegisState.Connecting => Theme.Warning,
        RegisState.Paused => Theme.Danger,
        _ => Theme.Muted,
    };

    private void UpdateThemeButton()
    {
        _themeButton.Text = Theme.Dark ? "☀" : "☾";
        _tip.SetToolTip(_themeButton, Theme.Dark ? Loc.T("Passer au thème clair") : Loc.T("Passer au thème sombre"));
    }

    /// <summary>Réapplique le thème (clair/sombre) après un changement.</summary>
    public void ReapplyTheme()
    {
        SendMessage(Handle, 0x000B, IntPtr.Zero, IntPtr.Zero); // on fige le dessin pendant le changement : un seul rafraîchissement à la fin
        try
        {
            Theme.Apply(this);
            UpdateThemeButton(); ApplyStateColors();
            _scenes.ThemeChanged(); _sounds.ThemeChanged();  // dessinées à la main : elles relisent les couleurs, rien à reconstruire
            RefreshView();
        }
        finally
        {
            SendMessage(Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            Invalidate(true);
        }
    }

    private void UpdateResources()
    {
        _meter.Sample();
        _resources.Text = _meter.Text;
        Text = $"OK Régis — {StateText(_o.State)} — {_meter.Text}"; // visible au survol dans la barre des tâches
    }

    /// <summary>Remet l'affichage à jour (à appeler depuis le thread de l'interface).</summary>
    public void RefreshView()
    {
        if (!Visible) return;
        _state.Text = StateText(_o.State);
        ApplyStateColors();
        _lastHeard.Text = Loc.T("Dernière phrase entendue : ") + (_o.LastHeard.Length > 0 ? _o.LastHeard : "—");
        PauseButton.Text = _o.State == RegisState.Paused ? Loc.T("Reprendre l'écoute") : Loc.T("Mettre en pause");

        _history.BeginUpdate(); _history.Items.Clear();
        foreach (var h in _o.History.AsEnumerable().Reverse().Take(100))
        {
            var item = new ListViewItem(new[] { h.At.ToString("HH:mm:ss"), h.Heard, h.Result });
            item.ForeColor = h.Success ? Theme.Text : Theme.Muted;
            _history.Items.Add(item);
        }
        _history.EndUpdate();
        _scenes.RebuildIfChanged();
        _sounds.RebuildIfChanged();
    }
}
