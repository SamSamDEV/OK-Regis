using OkRegis.Core.Commands;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>Fenêtre principale : état, scènes et surnoms (une carte par nom), réponses de Régis, historique.</summary>
public class MainForm : Form
{
    // Les actions pour lesquelles Régis répond (clé = clé dans AppSettings.Responses).
    private static readonly (string Key, string Title)[] ActionRows =
    {
        ("scene", "Quand je change de scène"), ("show", "Quand j'affiche une source"), ("hide", "Quand je masque une source"),
        ("mute", "Quand je coupe un son"), ("unmute", "Quand je rétablis un son"),
        ("volumeUp", "Quand je monte le volume"), ("volumeDown", "Quand je baisse le volume"),
        ("recordStart", "Quand je lance l'enregistrement"), ("recordStop", "Quand j'arrête l'enregistrement"),
        ("recordPause", "Quand je mets l'enregistrement en pause"),
        ("streamStart", "Quand je lance le stream"), ("streamStop", "Quand j'arrête le stream"),
        ("failed", "Quand ça n'a pas marché"),
    };
    private static readonly string[] ModeLabels = { "Voix de Régis (ou mes sons)", "Juste un bip", "Silence" };

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
    private readonly CardList _scenes = new() { Dock = DockStyle.Fill };
    private readonly CardList _responses = new() { Dock = DockStyle.Fill };
    private readonly ListView _history = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
    private readonly Label _resources = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", Padding = new Padding(16, 0, 0, 0) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 }; // actif seulement quand la fenêtre est visible
    private string _scenesSignature = "";

    public bool Exiting { get; set; }
    public Button PauseButton { get; } = new() { Text = "Mettre en pause", AutoSize = true };
    public Button SettingsButton { get; } = new() { Text = "Réglages", AutoSize = true };

    public MainForm(Orchestrator orchestrator, AppSettings settings, Vocabulary vocab, ResourceMeter meter,
        Action nicknamesChanged, Action save, Func<string, string, Task> preview, Action toggleTheme)
    {
        _o = orchestrator; _settings = settings; _vocab = vocab; _meter = meter;
        _nicknamesChanged = nicknamesChanged; _save = save; _preview = preview; _toggleTheme = toggleTheme;
        Text = "OK Régis"; ClientSize = new Size(840, 620); MinimumSize = new Size(720, 520); StartPosition = FormStartPosition.CenterScreen;
        Icon = TrayIcons.For(RegisState.Listening);

        _history.Columns.Add("Heure", 80); _history.Columns.Add("Entendu", 300); _history.Columns.Add("Résultat", 380);
        _themeButton.Click += (_, _) => _toggleTheme();

        var tabs = new NavTabs();
        tabs.Add("Scènes et surnoms", _scenes);
        tabs.Add("Réponses de Régis", _responses);
        tabs.Add("Historique", _history);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        footer.Controls.Add(_resources);
        Controls.Add(tabs); Controls.Add(BuildHeader()); Controls.Add(footer);

        BuildResponses();
        Theme.Apply(this);
        UpdateThemeButton();
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

    // ---- en-tête ----

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 92, Tag = "card", Padding = new Padding(22, 14, 22, 10) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 400, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0), Tag = "card" };
        buttons.Controls.Add(_themeButton); buttons.Controls.Add(SettingsButton); buttons.Controls.Add(PauseButton);
        _state.Font = Theme.TitleFont;
        var stateRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Tag = "card" };
        stateRow.Controls.Add(_dot); stateRow.Controls.Add(_state);
        _lastHeard.Dock = DockStyle.Top; _lastHeard.Padding = new Padding(4, 0, 0, 0);
        header.Controls.Add(_lastHeard); header.Controls.Add(stateRow); header.Controls.Add(buttons);
        return header;
    }

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

    private void UpdateThemeButton()
    {
        _themeButton.Text = Theme.Dark ? "☀" : "☾";
        _tip.SetToolTip(_themeButton, Theme.Dark ? "Passer au thème clair" : "Passer au thème sombre");
    }

    /// <summary>Réapplique le thème (clair/sombre) après un changement.</summary>
    public void ReapplyTheme()
    {
        Theme.Apply(this);
        UpdateThemeButton(); ApplyStateColors();
        _scenesSignature = ""; // force la reconstruction des cartes avec les nouvelles couleurs
        BuildResponses();
        RefreshView();
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
        RebuildScenesIfChanged();
    }

    // ---- scènes et surnoms : une carte par nom ----

    private IEnumerable<string> NicknamesOf(string realName) =>
        _settings.Nicknames.Where(n => Normalizer.Normalize(n.Value) == Normalizer.Normalize(realName)).Select(n => n.Key);

    // On ne reconstruit les cartes que si leur contenu change (sinon on perdrait le texte en cours de saisie).
    private void RebuildScenesIfChanged()
    {
        var snap = _o.Snapshot;
        var signature = snap is null ? "none" : string.Join("|", snap.Scenes) + "#" + snap.CurrentScene + "#"
            + string.Join("|", snap.SourcesInCurrentScene) + "#" + string.Join("|", snap.AudioSources) + "#"
            + string.Join("|", _settings.Nicknames.Select(n => n.Key + "=" + n.Value)) + "#"
            + string.Join("|", _o.Grammar?.Unspeakable ?? Array.Empty<string>());
        if (signature == _scenesSignature) return;
        _scenesSignature = signature;

        var items = new List<Control>();
        if (snap is null)
        {
            items.Add(BuildMessageCard("Ouvre OBS : tes scènes et sources apparaîtront ici.\nTu pourras alors leur donner des surnoms."));
        }
        else
        {
            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            void Section(string title, IReadOnlyList<string> names, Func<string, string> subtitle)
            {
                if (names.Count == 0) return;
                items.Add(new Label { Text = title, Tag = "muted", AutoSize = false, Height = 30, Font = new Font("Segoe UI Semibold", 9f), Padding = new Padding(6, 8, 0, 0) });
                var seen = new HashSet<string>();
                foreach (var name in names)
                {
                    var dup = !seen.Add(Normalizer.Normalize(name));
                    items.Add(BuildNameCard(name, subtitle(name), bad.Contains(name) || Normalizer.Normalize(name).Length == 0, dup));
                }
            }
            Section("SCÈNES", snap.Scenes, n => n == snap.CurrentScene ? "● affichée en ce moment" : "");
            Section("SOURCES DE LA SCÈNE AFFICHÉE", snap.SourcesInCurrentScene, _ => "");
            Section("SOURCES AUDIO", snap.AudioSources, _ => "");
        }
        _scenes.SetItems(items);
        Theme.Apply(_scenes);
    }

    private static Control BuildMessageCard(string text)
    {
        var card = new Card { Size = new Size(700, 100) };
        card.Controls.Add(new Label { Text = text, Tag = "muted", AutoSize = false, Location = new Point(20, 16), Size = new Size(660, 70), Font = Theme.BaseFont });
        return card;
    }

    // [NOM]  [pastilles de surnoms]            [ écris un surnom… ] [Ajouter]
    private Control BuildNameCard(string name, string subtitle, bool unspeakable, bool duplicate)
    {
        const int W = 700;
        var card = new Card { Size = new Size(W, 96) };
        var title = new Label { Text = name.Length > 0 ? name : "(nom vide)", Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(18, 12) };
        card.Controls.Add(title);
        if (subtitle.Length > 0)
        {
            var x = 18 + TextRenderer.MeasureText(title.Text, Theme.CardTitleFont).Width + 8;
            card.Controls.Add(new Label { Text = subtitle, Tag = "accent", AutoSize = true, Font = Theme.SmallFont, Location = new Point(x, 17) });
        }

        var chips = new FlowLayoutPanel { Location = new Point(18, 50), Size = new Size(W - 350, 34), WrapContents = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Tag = "card" };
        var nicknames = NicknamesOf(name).ToList();
        foreach (var nick in nicknames)
        {
            var chip = new Chip(nick);
            chip.Removed += () => { _settings.Nicknames.Remove(nick); _nicknamesChanged(); };
            chips.Controls.Add(chip);
        }
        if (nicknames.Count == 0)
        {
            var note = unspeakable
                ? new Label { Text = "⚠ Régis ne peut pas entendre ce nom : écris un surnom →", Tag = "danger", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }
                : duplicate
                    ? new Label { Text = "⚠ Nom en double : Régis utilisera le premier", Tag = "danger", AutoSize = true, Margin = new Padding(0, 6, 0, 0) }
                    : new Label { Text = "Pas de surnom : dis simplement le nom", Tag = "muted", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            chips.Controls.Add(note);
        }
        card.Controls.Add(chips);

        var box = new TextBox { Location = new Point(W - 320, 14), Size = new Size(206, 28), PlaceholderText = "Écris un surnom…", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var add = new Button { Text = "Ajouter", Tag = "primary", Location = new Point(W - 106, 12), Size = new Size(88, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var error = new Label { Tag = "danger", Font = Theme.SmallFont, Location = new Point(W - 320, 48), Size = new Size(302, 40), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        void Add()
        {
            var text = box.Text.Trim();
            var problem = text.Length == 0 ? "Écris d'abord un mot." : ValidateNickname(text, name);
            if (problem != null) { error.Text = problem; return; }
            _settings.Nicknames[text] = name;
            _nicknamesChanged();
        }
        add.Click += (_, _) => Add();
        box.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Add(); } };
        box.TextChanged += (_, _) => error.Text = "";
        card.Controls.AddRange(new Control[] { box, add, error });
        return card;
    }

    private string? ValidateNickname(string text, string realName)
    {
        if (!_vocab.CanSpeak(text, out _)) return $"Régis ne connaît pas « {text} ». Essaie un mot plus courant.";
        var key = Normalizer.Normalize(text);
        var snap = _o.Snapshot;
        var all = (snap?.Scenes ?? Array.Empty<string>()).Concat(snap?.SourcesInCurrentScene ?? Array.Empty<string>()).Concat(snap?.AudioSources ?? Array.Empty<string>());
        if (all.Any(n => Normalizer.Normalize(n) == key && n != realName)) return "Ce mot est déjà le nom d'une autre scène ou source.";
        if (_settings.Nicknames.TryGetValue(text, out var other) && Normalizer.Normalize(other) != Normalizer.Normalize(realName))
            return $"Ce surnom est déjà utilisé pour « {other} ».";
        return null;
    }

    // ---- réponses de Régis : une carte par action ----

    private void BuildResponses()
    {
        var items = new List<Control>();

        // Carte du haut : comment Régis répond.
        var top = new Card { Size = new Size(700, 118) };
        top.Controls.Add(new Label { Text = "Comment Régis répond ?", Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(18, 12) });
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(18, 44), Size = new Size(260, 28) };
        mode.Items.AddRange(ModeLabels);
        mode.SelectedIndex = _settings.VoiceMode switch { ResponseMode.Beep => 1, ResponseMode.Silent => 2, _ => 0 };
        mode.SelectedIndexChanged += (_, _) =>
        {
            _settings.VoiceMode = mode.SelectedIndex switch { 1 => ResponseMode.Beep, 2 => ResponseMode.Silent, _ => ResponseMode.WindowsVoice };
            _save();
        };
        top.Controls.Add(mode);
        top.Controls.Add(new Label
        {
            Text = "Écris la phrase que Régis dit ({scène} et {source} sont remplacés par le nom). Si tu choisis un fichier son, il passe avant la voix.",
            Tag = "muted", Font = Theme.SmallFont, AutoSize = false, Location = new Point(18, 76), Size = new Size(664, 34), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });
        items.Add(top);

        foreach (var (key, title) in ActionRows) items.Add(BuildResponseCard(key, title));
        _responses.SetItems(items);
        Theme.Apply(_responses);
    }

    private Control BuildResponseCard(string key, string title)
    {
        const int W = 700;
        var card = new Card { Size = new Size(W, 118) };
        card.Controls.Add(new Label { Text = title, Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(18, 12) });

        var phrase = new TextBox
        {
            Location = new Point(18, 44), Size = new Size(W - 36, 28), PlaceholderText = "Phrase que Régis dit…",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = _settings.Responses.TryGetValue(key, out var list) ? string.Join(" | ", list) : "",
        };
        void Commit()
        {
            var phrases = phrase.Text.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (phrases.Count > 0) _settings.Responses[key] = phrases; else _settings.Responses.Remove(key);
            _save();
        }
        phrase.Leave += (_, _) => Commit();
        phrase.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); } };

        var listen = new Button { Text = "▶  Écouter", Location = new Point(18, 78), Size = new Size(110, 32) };
        listen.Click += async (_, _) =>
        {
            Commit();
            await _preview(key, new ResponsePicker(_settings).Pick(key, "LoL"));
        };

        var hasFile = _settings.CustomResponseFiles.TryGetValue(key, out var file) && file.Length > 0;
        var choose = new Button { Text = hasFile ? "Changer le son…" : "Choisir un son…", Location = new Point(136, 78), Size = new Size(150, 32) };
        choose.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Choisir un son", Filter = "Sons (*.wav;*.mp3)|*.wav;*.mp3" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _settings.CustomResponseFiles[key] = dialog.FileName;
            _save(); BuildResponses();
        };

        var fileLabel = new Label
        {
            Text = hasFile ? "♪  " + Path.GetFileName(file) : "Aucun son : Régis utilise sa voix",
            Tag = hasFile ? "accent" : "muted", AutoSize = false, Location = new Point(296, 84), Size = new Size(W - 296 - 60, 22),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        card.Controls.AddRange(new Control[] { phrase, listen, choose, fileLabel });

        if (hasFile)
        {
            var remove = new Button { Text = "✕", Location = new Point(W - 50, 78), Size = new Size(34, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _tip.SetToolTip(remove, "Retirer ce son");
            remove.Click += (_, _) => { _settings.CustomResponseFiles.Remove(key); _save(); BuildResponses(); };
            card.Controls.Add(remove);
        }
        return card;
    }
}
