using OkRegis.Core.Commands;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>
/// Les scènes comme dans OBS : une carte par scène avec ses sources dessous, plus une carte « Sons » (muet en un clic).
/// Chaque nom a ses surnoms (pastilles) et un champ pour en ajouter. Les scènes et les sources se réordonnent à la souris
/// (l'ordre est celui de l'appli : OBS n'est pas modifié).
/// </summary>
public class ScenesView : CardList
{
    private const string AudioKey = "(audio)";

    private readonly Orchestrator _o;
    private readonly AppSettings _settings;
    private readonly Vocabulary _vocab;
    private readonly Action _nicknamesChanged; // enregistre et reconstruit la grammaire (en arrière-plan)
    private readonly Action _save;             // enregistre seulement

    private readonly HashSet<string> _expanded = new();   // scènes dépliées
    private readonly Dictionary<string, bool> _muted = new();
    private readonly Dictionary<string, List<Button>> _muteButtons = new();
    private bool _expandInitialized;
    private string _signature = "";

    public ScenesView(Orchestrator o, AppSettings settings, Vocabulary vocab, Action nicknamesChanged, Action save)
    {
        _o = o; _settings = settings; _vocab = vocab; _nicknamesChanged = nicknamesChanged; _save = save;
    }

    /// <summary>Force la reconstruction au prochain RebuildIfChanged (ex. après un changement de thème).</summary>
    public void Invalidate() => _signature = "";

    // On ne reconstruit que si le contenu change (sinon on perdrait le texte en cours de saisie et l'appli ramerait).
    public void RebuildIfChanged()
    {
        var signature = Signature();
        if (signature == _signature) return;
        _signature = signature;
        Build();
    }

    private string Signature()
    {
        var snap = _o.Snapshot;
        if (snap is null) return "none" + Theme.Dark;
        return string.Join("|", snap.Scenes) + "#" + snap.CurrentScene + "#" + string.Join("|", snap.AudioSources) + "#"
            + string.Join("|", snap.SourcesByScene?.Select(p => p.Key + ":" + string.Join(",", p.Value)) ?? Array.Empty<string>()) + "#"
            + string.Join("|", _settings.Nicknames.Select(n => n.Key + "=" + n.Value)) + "#"
            + string.Join("|", _o.Grammar?.Unspeakable ?? Array.Empty<string>()) + "#"
            + string.Join("|", _expanded) + "#" + Theme.Dark;
    }

    // ---- construction ----

    private void Build()
    {
        var snap = _o.Snapshot;
        var items = new List<Control>();
        _muteButtons.Clear();

        if (snap is null)
        {
            items.Add(MessageCard("Ouvre OBS : tes scènes et leurs sources apparaîtront ici.\nTu pourras leur donner des surnoms et couper un son en un clic."));
        }
        else
        {
            if (!_expandInitialized) { if (snap.CurrentScene != null) _expanded.Add(snap.CurrentScene); _expandInitialized = true; }
            _muted.Clear();
            foreach (var (name, isMuted) in snap.AudioMuted ?? new Dictionary<string, bool>()) _muted[name] = isMuted;

            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            if (snap.AudioSources.Count > 0) items.Add(BuildAudioGroup(snap, bad));
            foreach (var scene in OrderHelper.Apply(snap.Scenes, _settings.SceneOrder)) items.Add(BuildSceneGroup(scene, snap, bad));
        }

        SetItems(items);
        Theme.Apply(this);
        foreach (var group in items.OfType<GroupCard>()) group.FitChildren();
        UpdateMuteButtons();
    }

    private static Control MessageCard(string text)
    {
        var card = new Card { Size = new Size(700, 100) };
        card.Controls.Add(new Label { Text = text, Tag = "muted", AutoSize = false, Location = new Point(20, 16), Size = new Size(660, 70), Font = Theme.BaseFont });
        return card;
    }

    private GroupCard BuildAudioGroup(ObsSnapshot snap, HashSet<string> bad)
    {
        var group = new GroupCard { Size = new Size(700, 80) };
        var header = new Panel { Size = new Size(692, 58), Tag = "card" };
        header.Controls.Add(new Label { Text = "Sons (audio)", Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(18, 10) });
        header.Controls.Add(new Label { Text = "Coupe ou rétablis un son d'un clic. À la voix : « coupe le micro », « mute discord »…", Tag = "muted", Font = Theme.SmallFont, AutoSize = true, Location = new Point(18, 36) });
        group.Stack.Controls.Add(header);
        group.Stack.Controls.Add(BuildRows(AudioKey, snap.AudioSources, audio: true, bad, group));
        return group;
    }

    private GroupCard BuildSceneGroup(string scene, ObsSnapshot snap, HashSet<string> bad)
    {
        const int W = 692;
        var group = new GroupCard { Tag = scene, Size = new Size(700, 100) };
        var expanded = _expanded.Contains(scene);
        var sources = snap.SourcesByScene != null && snap.SourcesByScene.TryGetValue(scene, out var s) ? s : Array.Empty<string>();

        var header = new Panel { Size = new Size(W, 96), Tag = "card" };
        var handle = new Label { Text = "⠿", Font = Theme.SymbolFont14, Tag = "muted", AutoSize = true, Location = new Point(10, 30) };
        var chevron = new Label { Text = expanded ? "▾" : "▸", Font = Theme.SymbolFont12, AutoSize = true, Location = new Point(38, 31), Cursor = Cursors.Hand, Tag = "muted" };
        void Toggle() { if (!_expanded.Remove(scene)) _expanded.Add(scene); RebuildIfChanged(); }
        chevron.Click += (_, _) => Toggle();
        var title = new Label { Text = scene.Length > 0 ? scene : "(nom vide)", Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(64, 12), Cursor = Cursors.Hand };
        title.Click += (_, _) => Toggle();
        header.Controls.AddRange(new Control[] { handle, chevron, title });

        var x = 64 + TextRenderer.MeasureText(title.Text, Theme.CardTitleFont).Width + 8;
        var info = (scene == snap.CurrentScene ? "● affichée en ce moment   " : "") + $"{sources.Count} source{(sources.Count > 1 ? "s" : "")}";
        header.Controls.Add(new Label { Text = info, Tag = scene == snap.CurrentScene ? "accent" : "muted", AutoSize = true, Font = Theme.SmallFont, Location = new Point(x, 17) });

        AddNicknameBlock(header, scene, W, chipsX: 64, chipsY: 50, boxY: 14, errorY: 50, unspeakable: bad.Contains(scene) || Normalizer.Normalize(scene).Length == 0, duplicate: false, short_: false);
        group.Stack.Controls.Add(header);
        if (expanded) group.Stack.Controls.Add(BuildRows(scene, sources, audio: false, bad, group));

        DragReorder.Attach(handle, group, order => { _settings.SceneOrder = order.ToList(); _save(); _signature = Signature(); },
            on => { group.Highlight = on; });
        return group;
    }

    // Les lignes de sources d'une scène (ou des sons), dans l'ordre choisi par l'utilisateur.
    private FlowLayoutPanel BuildRows(string orderKey, IReadOnlyList<string> names, bool audio, HashSet<string> bad, GroupCard group)
    {
        var rows = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Tag = "rows", Margin = Padding.Empty, Width = 692 };
        var ordered = OrderHelper.Apply(names, _settings.SourceOrder.GetValueOrDefault(orderKey));
        var seen = new HashSet<string>();
        foreach (var name in ordered)
        {
            var duplicate = !seen.Add(Normalizer.Normalize(name));
            var row = BuildRow(name, audio, bad.Contains(name) || Normalizer.Normalize(name).Length == 0, duplicate);
            row.Margin = Padding.Empty;
            rows.Controls.Add(row);
            var handle = row.Controls.OfType<Label>().First(l => l.Text == "⠿");
            DragReorder.Attach(handle, row,
                order => { _settings.SourceOrder[orderKey] = order.ToList(); _save(); _signature = Signature(); },
                on => row.Dragging = on);
        }
        return rows;
    }

    // [⠿] NOM   [pastilles]        [Muet] [ écris un surnom… ] [Ajouter]
    private RowPanel BuildRow(string name, bool audio, bool unspeakable, bool duplicate)
    {
        const int W = 692;
        var row = new RowPanel { Size = new Size(W, 64), Tag = name };
        row.Controls.Add(new Label { Text = "⠿", Font = Theme.SymbolFont12, Tag = "muted", AutoSize = true, Location = new Point(34, 20) });
        var nameWidth = audio ? 150 : 190;
        row.Controls.Add(new Label { Text = name.Length > 0 ? name : "(nom vide)", AutoSize = false, AutoEllipsis = true, Size = new Size(nameWidth, 22), Location = new Point(62, 21), Font = Theme.BaseFont });
        var chipsX = 62 + nameWidth + 6;
        if (audio)
        {
            chipsX += 108 + 6; // le bouton muet se place juste après le nom
            var mute = new Button { Size = new Size(108, 32), Location = new Point(62 + nameWidth + 6, 12) };
            mute.Click += async (_, _) =>
            {
                mute.Enabled = false;
                try { _muted[name] = await _o.ToggleMuteAsync(name); }
                catch (Exception) { /* OBS fermé : on ne change rien */ }
                finally { mute.Enabled = true; UpdateMuteButtons(); }
            };
            if (!_muteButtons.TryGetValue(name, out var list)) _muteButtons[name] = list = new List<Button>();
            list.Add(mute);
            row.Controls.Add(mute);
        }
        AddNicknameBlock(row, name, W, chipsX, chipsY: 17, boxY: 12, errorY: 42, unspeakable, duplicate, short_: true);
        return row;
    }

    // Pastilles de surnoms + champ « Écris un surnom… » + bouton « Ajouter », pour un nom donné.
    private void AddNicknameBlock(Control host, string name, int width, int chipsX, int chipsY, int boxY, int errorY,
        bool unspeakable, bool duplicate, bool short_)
    {
        const int rightPart = 276;
        var chips = new FlowLayoutPanel
        {
            Location = new Point(chipsX, chipsY), Size = new Size(Math.Max(60, width - chipsX - rightPart), 34), WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Tag = "card",
        };
        var nicknames = NicknamesOf(name).ToList();
        foreach (var nick in nicknames)
        {
            var chip = new Chip(nick);
            chip.Removed += () => { _settings.Nicknames.Remove(nick); _nicknamesChanged(); };
            chips.Controls.Add(chip);
        }
        if (nicknames.Count == 0)
        {
            Label? note = null;
            if (unspeakable) note = new Label { Text = short_ ? "⚠ Régis ne peut pas entendre ce nom → surnom" : "⚠ Régis ne peut pas entendre ce nom : écris un surnom →", Tag = "danger", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            else if (duplicate) note = new Label { Text = "⚠ Nom en double : Régis utilisera le premier", Tag = "danger", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            else if (!short_) note = new Label { Text = "Pas de surnom : dis simplement le nom", Tag = "muted", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            if (note != null) chips.Controls.Add(note);
        }
        host.Controls.Add(chips);

        var box = new TextBox { Location = new Point(width - 266, boxY), Size = new Size(166, 28), PlaceholderText = "Écris un surnom…", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var add = new Button { Text = "Ajouter", Tag = "primary", Location = new Point(width - 94, boxY - 2), Size = new Size(80, 32), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var error = new Label { Tag = "danger", Font = Theme.SmallFont, Location = new Point(width - 266, errorY), Size = new Size(252, short_ ? 18 : 40), Anchor = AnchorStyles.Top | AnchorStyles.Right };
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
        host.Controls.AddRange(new Control[] { box, add, error });
    }

    private IEnumerable<string> NicknamesOf(string realName) =>
        _settings.Nicknames.Where(n => Normalizer.Normalize(n.Value) == Normalizer.Normalize(realName)).Select(n => n.Key);

    private string? ValidateNickname(string text, string realName)
    {
        if (!_vocab.CanSpeak(text, out _)) return $"Régis ne connaît pas « {text} ». Essaie un mot plus courant.";
        var key = Normalizer.Normalize(text);
        var snap = _o.Snapshot;
        var all = (snap?.Scenes ?? Array.Empty<string>()).Concat(snap?.AudioSources ?? Array.Empty<string>())
            .Concat(snap?.SourcesByScene?.Values.SelectMany(v => v) ?? Array.Empty<string>());
        if (all.Any(n => Normalizer.Normalize(n) == key && n != realName)) return "Ce mot est déjà le nom d'une autre scène ou source.";
        if (_settings.Nicknames.TryGetValue(text, out var other) && Normalizer.Normalize(other) != Normalizer.Normalize(realName))
            return $"Ce surnom est déjà utilisé pour « {other} ».";
        return null;
    }

    // ---- boutons muet : mis à jour sur place, sans tout reconstruire ----

    public void UpdateMuteButtons()
    {
        foreach (var (name, buttons) in _muteButtons)
        {
            var muted = _muted.TryGetValue(name, out var m) && m;
            foreach (var b in buttons)
            {
                b.Text = muted ? "🔇  Coupé" : "🔊  Actif";
                b.BackColor = muted ? Theme.Danger : Theme.SurfaceAlt;
                b.ForeColor = muted ? Color.White : Theme.Text;
                b.FlatAppearance.MouseOverBackColor = muted ? ControlPaint.Light(Theme.Danger, 0.15f) : Theme.Border;
            }
        }
    }
}
