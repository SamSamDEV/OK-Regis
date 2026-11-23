using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>Fenêtre principale : état, historique, scènes/sources avec leurs surnoms, consommation en direct.</summary>
public class MainForm : Form
{
    private readonly Orchestrator _o;
    private readonly AppSettings _settings;
    private readonly Vocabulary _vocab;
    private readonly ResourceMeter _meter;
    private readonly Action _nicknamesChanged; // enregistre et reconstruit la grammaire

    private readonly Label _dot = new() { Text = "●", AutoSize = true, Font = new Font("Segoe UI", 16f) };
    private readonly Label _state = new() { AutoSize = true };
    private readonly Label _lastHeard = new() { AutoSize = true, Tag = "muted" };
    private readonly ListView _history = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    private readonly ListView _names = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true, MultiSelect = false, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    private readonly Button _add = new() { Text = "＋  Ajouter un surnom", Tag = "primary", AutoSize = true, Enabled = false };
    private readonly Button _remove = new() { Text = "Retirer un surnom", AutoSize = true, Enabled = false };
    private readonly Label _hint = new() { AutoSize = true, Tag = "muted", Text = "Sélectionne une scène ou une source dans la liste (ou double-clique dessus).", Padding = new Padding(8, 8, 0, 0) };
    private readonly Label _resources = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", Padding = new Padding(12, 0, 0, 0) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 }; // actif seulement quand la fenêtre est visible

    public bool Exiting { get; set; }
    public Button PauseButton { get; } = new() { Text = "Mettre en pause", AutoSize = true };
    public Button SettingsButton { get; } = new() { Text = "Réglages", AutoSize = true };

    public MainForm(Orchestrator orchestrator, AppSettings settings, Vocabulary vocab, ResourceMeter meter, Action nicknamesChanged)
    {
        _o = orchestrator; _settings = settings; _vocab = vocab; _meter = meter; _nicknamesChanged = nicknamesChanged;
        Text = "OK Régis"; ClientSize = new Size(780, 560); MinimumSize = new Size(680, 480); StartPosition = FormStartPosition.CenterScreen;
        Icon = TrayIcons.For(RegisState.Listening);

        _history.Columns.Add("Heure", 80); _history.Columns.Add("Entendu", 280); _history.Columns.Add("Résultat", 340);
        _names.Columns.Add("Type", 80); _names.Columns.Add("Nom dans OBS", 220); _names.Columns.Add("Surnoms", 180); _names.Columns.Add("Remarque", 240);
        _names.DoubleClick += (_, _) => AddNickname();
        _names.SelectedIndexChanged += (_, _) => UpdateButtons();
        _add.Click += (_, _) => AddNickname();
        _remove.Click += (_, _) => RemoveNickname();

        Controls.Add(BuildBody()); Controls.Add(BuildHeader()); Controls.Add(BuildFooter());
        Theme.Apply(this);
        ApplyStateColors();

        _timer.Tick += (_, _) => UpdateResources();
        // Le minuteur ne tourne que si la fenêtre est affichée : zéro travail quand elle est cachée ou réduite.
        VisibleChanged += (_, _) => UpdateTimer();
        Resize += (_, _) => UpdateTimer();
        FormClosing += (_, e) => { if (!Exiting) { e.Cancel = true; Hide(); } };
    }

    private void UpdateTimer()
    {
        var active = Visible && WindowState != FormWindowState.Minimized;
        _timer.Enabled = active;
        if (active) { RefreshView(); UpdateResources(); }
    }

    // ---- construction de la fenêtre ----

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 92, Tag = "card", Padding = new Padding(20, 14, 20, 10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 330, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(SettingsButton); buttons.Controls.Add(PauseButton);
        _state.Font = Theme.TitleFont;
        var stateRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, AutoSize = false };
        stateRow.Controls.Add(_dot); stateRow.Controls.Add(_state);
        _lastHeard.Dock = DockStyle.Top; _lastHeard.Padding = new Padding(4, 0, 0, 0);
        header.Controls.Add(_lastHeard); header.Controls.Add(stateRow); header.Controls.Add(buttons);
        return header;
    }

    private Control BuildBody()
    {
        var tabs = new NavTabs();
        tabs.Add("Historique", _history);

        var namesPage = new Panel();
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 6, 0, 0) };
        bar.Controls.AddRange(new Control[] { _add, _remove, _hint });
        namesPage.Controls.Add(_names); namesPage.Controls.Add(bar);
        tabs.Add("Scènes et sources", namesPage);
        return tabs;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        footer.Controls.Add(_resources);
        return footer;
    }

    // ---- affichage ----

    public static string StateText(RegisState s) => s switch
    {
        RegisState.Idle => "En veille (OBS fermé)",
        RegisState.Connecting => "Connexion à OBS…",
        RegisState.Listening => "À l'écoute",
        _ => "En pause",
    };

    private void ApplyStateColors() => _dot.ForeColor = _o.State switch
    {
        RegisState.Listening => Theme.Success,
        RegisState.Connecting => Theme.Warning,
        RegisState.Paused => Theme.Danger,
        _ => Theme.Muted,
    };

    /// <summary>Réapplique le thème (clair/sombre) après un changement de réglage.</summary>
    public void ReapplyTheme() { Theme.Apply(this); ApplyStateColors(); RefreshView(); }

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
        _lastHeard.Text = "Dernière phrase entendue : " + (_o.LastHeard.Length > 0 ? _o.LastHeard : "—");
        PauseButton.Text = _o.State == RegisState.Paused ? "Reprendre l'écoute" : "Mettre en pause";

        _history.BeginUpdate(); _history.Items.Clear();
        foreach (var h in _o.History.AsEnumerable().Reverse().Take(100))
        {
            var item = new ListViewItem(new[] { h.At.ToString("HH:mm:ss"), h.Heard, h.Result });
            item.ForeColor = h.Success ? Theme.Text : Theme.Muted;
            _history.Items.Add(item);
        }
        _history.EndUpdate();
        RefreshNames();
    }

    private void RefreshNames()
    {
        var selected = _names.SelectedItems.Count > 0 ? _names.SelectedItems[0].SubItems[1].Text : null;
        _names.BeginUpdate(); _names.Items.Clear();
        var snap = _o.Snapshot;
        if (snap != null)
        {
            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            void AddAll(string type, IReadOnlyList<string> names)
            {
                var seen = new HashSet<string>();
                foreach (var name in names)
                {
                    var key = Normalizer.Normalize(name);
                    var nicknames = string.Join(", ", NicknamesOf(name));
                    var note = bad.Contains(name) ? "⚠ Régis ne peut pas entendre ce nom : ajoute un surnom"
                        : key.Length == 0 ? "⚠ Nom sans lettre ni chiffre : ajoute un surnom"
                        : !seen.Add(key) ? "⚠ Nom en double : Régis utilisera le premier"
                        : "";
                    var item = new ListViewItem(new[] { type, name, nicknames, note });
                    item.ForeColor = note.Length > 0 ? Theme.Danger : Theme.Text;
                    _names.Items.Add(item);
                    if (name == selected) item.Selected = true;
                }
            }
            AddAll("Scène", snap.Scenes);
            AddAll("Source", snap.SourcesInCurrentScene);
            AddAll("Audio", snap.AudioSources);
        }
        _names.EndUpdate();
        UpdateButtons();
    }

    // ---- surnoms ----

    private IEnumerable<string> NicknamesOf(string realName) =>
        _settings.Nicknames.Where(n => Normalizer.Normalize(n.Value) == Normalizer.Normalize(realName)).Select(n => n.Key);

    private string? SelectedName => _names.SelectedItems.Count > 0 ? _names.SelectedItems[0].SubItems[1].Text : null;

    private void UpdateButtons()
    {
        var name = SelectedName;
        _add.Enabled = name != null;
        _remove.Enabled = name != null && NicknamesOf(name).Any();
        _hint.Visible = name == null;
    }

    private void AddNickname()
    {
        var name = SelectedName;
        if (name == null) return;
        var allNames = (_o.Snapshot?.Scenes ?? Array.Empty<string>())
            .Concat(_o.Snapshot?.SourcesInCurrentScene ?? Array.Empty<string>())
            .Concat(_o.Snapshot?.AudioSources ?? Array.Empty<string>());

        var nick = Prompt.Ask("Ajouter un surnom",
            $"Quel mot veux-tu dire pour « {name} » ?\nChoisis un mot simple (ex. jeu, camera, musique).",
            validate: text =>
            {
                if (!_vocab.CanSpeak(text, out _)) return $"Régis ne connaît pas « {text} ». Essaie un autre mot, plus courant.";
                var key = Normalizer.Normalize(text);
                if (allNames.Any(n => Normalizer.Normalize(n) == key && n != name)) return "Ce mot est déjà le nom d'une autre scène ou source.";
                if (_settings.Nicknames.TryGetValue(text, out var other) && Normalizer.Normalize(other) != Normalizer.Normalize(name))
                    return $"Ce surnom est déjà utilisé pour « {other} ».";
                return null;
            }, owner: this);
        if (nick == null) return;
        _settings.Nicknames[nick] = name;
        _nicknamesChanged();
    }

    private void RemoveNickname()
    {
        var name = SelectedName;
        if (name == null) return;
        var menu = new ContextMenuStrip();
        foreach (var nick in NicknamesOf(name).ToList())
            menu.Items.Add($"Retirer « {nick} »", null, (_, _) => { _settings.Nicknames.Remove(nick); _nicknamesChanged(); });
        menu.Show(_remove, new Point(0, _remove.Height));
    }
}
