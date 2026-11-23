using OkRegis.Core.Orchestration;

namespace OkRegis.App;

/// <summary>Fenêtre principale : état, historique, noms détectés, consommation en direct.</summary>
public class MainForm : Form
{
    private readonly Orchestrator _o;
    private readonly ResourceMeter _meter;
    private readonly Action<string, string> _addNickname;   // (surnom, vrai nom)
    private readonly Label _state = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
    private readonly Label _lastHeard = new() { AutoSize = true };
    private readonly ListView _history = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true };
    private readonly ListView _names = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true };
    private readonly Label _resources = new() { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.FixedSingle };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 }; // actif seulement quand la fenêtre est visible

    public bool Exiting { get; set; }
    public Button PauseButton { get; } = new() { Text = "Mettre en pause", AutoSize = true };
    public Button SettingsButton { get; } = new() { Text = "Réglages…", AutoSize = true };

    public MainForm(Orchestrator orchestrator, ResourceMeter meter, Action<string, string> addNickname)
    {
        _o = orchestrator; _meter = meter; _addNickname = addNickname;
        Text = "OK Régis"; ClientSize = new Size(640, 520); StartPosition = FormStartPosition.CenterScreen;

        _history.Columns.Add("Heure", 70); _history.Columns.Add("Entendu", 230); _history.Columns.Add("Résultat", 280);
        _names.Columns.Add("Type", 70); _names.Columns.Add("Nom dans OBS", 230); _names.Columns.Add("Remarque", 290);
        _names.DoubleClick += (_, _) => AddNicknameForSelected();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 60, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8) };
        top.Controls.AddRange(new Control[] { _state, _lastHeard });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(4) };
        buttons.Controls.AddRange(new Control[] { PauseButton, SettingsButton });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var historyPage = new TabPage("Historique"); historyPage.Controls.Add(_history);
        var namesPage = new TabPage("Scènes et sources détectées (double-clic = ajouter un surnom)"); namesPage.Controls.Add(_names);
        tabs.TabPages.AddRange(new[] { historyPage, namesPage });

        Controls.Add(tabs); Controls.Add(buttons); Controls.Add(top); Controls.Add(_resources);

        _timer.Tick += (_, _) => UpdateResources();
        // Le minuteur ne tourne que si la fenêtre est visible : zéro travail quand elle est cachée.
        VisibleChanged += (_, _) => { _timer.Enabled = Visible; if (Visible) { RefreshView(); UpdateResources(); } };
        FormClosing += (_, e) => { if (!Exiting) { e.Cancel = true; Hide(); } };
    }

    public static string StateText(RegisState s) => s switch
    {
        RegisState.Idle => "En veille (OBS fermé)",
        RegisState.Connecting => "Connexion à OBS…",
        RegisState.Listening => "À l'écoute",
        _ => "En pause",
    };

    private void UpdateResources()
    {
        _meter.Sample();
        _resources.Text = " " + _meter.Text;
        Text = $"OK Régis — {StateText(_o.State)} — {_meter.Text}"; // visible au survol dans la barre des tâches
    }

    /// <summary>Remet l'affichage à jour (à appeler depuis le thread de l'interface).</summary>
    public void RefreshView()
    {
        if (!Visible) return;
        _state.Text = StateText(_o.State);
        _lastHeard.Text = "Dernière phrase entendue : " + (_o.LastHeard.Length > 0 ? _o.LastHeard : "—");
        PauseButton.Text = _o.State == RegisState.Paused ? "Reprendre l'écoute" : "Mettre en pause";

        _history.BeginUpdate(); _history.Items.Clear();
        foreach (var h in _o.History.AsEnumerable().Reverse().Take(100))
        {
            var item = new ListViewItem(new[] { h.At.ToString("HH:mm:ss"), h.Heard, h.Result });
            if (!h.Success) item.ForeColor = Color.Gray;
            _history.Items.Add(item);
        }
        _history.EndUpdate();

        _names.BeginUpdate(); _names.Items.Clear();
        var snap = _o.Snapshot;
        if (snap != null)
        {
            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            void Add(string type, string name)
            {
                var item = new ListViewItem(new[] { type, name, bad.Contains(name) ? "⚠ Régis ne peut pas entendre ce nom" : "" });
                if (bad.Contains(name)) item.ForeColor = Color.Firebrick;
                _names.Items.Add(item);
            }
            foreach (var n in snap.Scenes) Add("Scène", n);
            foreach (var n in snap.SourcesInCurrentScene) Add("Source", n);
            foreach (var n in snap.AudioSources) Add("Audio", n);
        }
        _names.EndUpdate();
    }

    private void AddNicknameForSelected()
    {
        if (_names.SelectedItems.Count == 0) return;
        var name = _names.SelectedItems[0].SubItems[1].Text;
        var nick = Prompt.Ask("Ajouter un surnom", $"Comment veux-tu appeler « {name} » à voix haute ?\n(un mot que Régis sait entendre, ex. « jeu »)");
        if (nick != null) _addNickname(nick, name);
    }
}
