using OkRegis.Core.Commands;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>
/// Les scènes comme dans OBS : une carte par scène avec ses sources dessous, plus une carte « Sons » (muet en un clic).
/// Chaque ligne affiche ses surnoms en pastilles et un bouton « + Surnom » (un seul champ de saisie partagé s'ouvre au clic).
/// Les scènes et les sources se réordonnent à la souris (l'ordre est celui de l'appli : OBS n'est pas modifié).
/// </summary>
public class ScenesView : Panel
{
    private const string AudioKey = "(audio)";

    private readonly Orchestrator _o;
    private readonly AppSettings _settings;
    private readonly Vocabulary _vocab;
    private readonly Action _nicknamesChanged; // enregistre et reconstruit la grammaire (en arrière-plan)
    private readonly Action _save;             // enregistre seulement

    private readonly CardList _list = new() { Dock = DockStyle.Fill };
    private readonly NicknameEditor _editor = new();
    private readonly HashSet<string> _expanded = new();   // scènes dépliées
    private readonly Dictionary<string, bool> _muted = new();
    private readonly Dictionary<string, List<NameRow>> _muteRows = new();
    private readonly Dictionary<string, (NameRow Header, int Count)> _headers = new();
    private bool _expandInitialized;
    private string _signature = "";

    public ScenesView(Orchestrator o, AppSettings settings, Vocabulary vocab, Action nicknamesChanged, Action save)
    {
        _o = o; _settings = settings; _vocab = vocab; _nicknamesChanged = nicknamesChanged; _save = save;
        Controls.Add(_editor); Controls.Add(_list);   // l'éditeur est au-dessus de la liste
        _list.Scrolled += () => _editor.Close();
        _editor.BringToFront();
    }

    /// <summary>La liste qui défile (utile pour les tests d'affichage).</summary>
    public CardList List => _list;

    /// <summary>Force la reconstruction au prochain RebuildIfChanged (ex. après un changement de thème).</summary>
    public void ForceRebuild() => _signature = "";

    // On ne reconstruit que si le contenu change (sinon on perdrait la saisie et l'appli ramerait).
    public void RebuildIfChanged()
    {
        var signature = Signature();
        if (signature != _signature) { _signature = signature; Build(); }
        UpdateCurrentScene(); // changer de scène dans OBS ne reconstruit rien : on met juste à jour les en-têtes
    }

    // Met à jour « ● affichée en ce moment » sur place.
    private void UpdateCurrentScene()
    {
        var current = _o.Snapshot?.CurrentScene;
        foreach (var (scene, (header, count)) in _headers)
        {
            var isCurrent = scene == current;
            var info = (isCurrent ? "● affichée en ce moment   " : "") + $"{count} source{(count > 1 ? "s" : "")}";
            if (header.Info == info) continue;
            header.Info = info; header.InfoAccent = isCurrent; header.Invalidate();
        }
    }

    // Déplier / replier une scène : seules ses lignes sont créées ou supprimées.
    private void ToggleGroup(string scene, GroupCard group, NameRow header)
    {
        var snap = _o.Snapshot;
        if (snap is null) return;
        var existing = group.Stack.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(c => c.Tag is "rows");
        if (_expanded.Remove(scene))
        {
            header.Expanded = false;
            if (existing != null) { group.Stack.Controls.Remove(existing); existing.Dispose(); }
        }
        else
        {
            _expanded.Add(scene); header.Expanded = true;
            var sources = snap.SourcesByScene != null && snap.SourcesByScene.TryGetValue(scene, out var s) ? s : Array.Empty<string>();
            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            var rows = BuildRows(scene, sources, NameRow.RowKind.Source, bad);
            group.Stack.Controls.Add(rows);
            Theme.Apply(rows);
        }
        header.Invalidate();
        group.FitChildren();
        _signature = Signature();
    }

    private string Signature()
    {
        var snap = _o.Snapshot;
        if (snap is null) return "none" + Theme.Dark;
        return string.Join("|", snap.Scenes) + "#" + string.Join("|", snap.AudioSources) + "#"
            + string.Join("|", snap.SourcesByScene?.Select(p => p.Key + ":" + string.Join(",", p.Value)) ?? Array.Empty<string>()) + "#"
            + string.Join("|", _settings.Nicknames.Select(n => n.Key + "=" + n.Value)) + "#"
            + string.Join("|", _o.Grammar?.Unspeakable ?? Array.Empty<string>()) + "#" + Theme.Dark;
    }

    // ---- construction ----

    private void Build()
    {
        _editor.Close();
        var snap = _o.Snapshot;
        var items = new List<Control>();
        _muteRows.Clear(); _headers.Clear();

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

        _list.SetItems(items);
        Theme.Apply(_list);
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
        group.Stack.Controls.Add(BuildRows(AudioKey, snap.AudioSources, NameRow.RowKind.Audio, bad));
        return group;
    }

    private GroupCard BuildSceneGroup(string scene, ObsSnapshot snap, HashSet<string> bad)
    {
        var group = new GroupCard { Tag = scene, Size = new Size(700, 100) };
        var expanded = _expanded.Contains(scene);
        var sources = snap.SourcesByScene != null && snap.SourcesByScene.TryGetValue(scene, out var s) ? s : Array.Empty<string>();

        var current = scene == snap.CurrentScene;
        var header = new NameRow
        {
            Kind = NameRow.RowKind.Scene, Title = scene, Expanded = expanded, Height = 60, Width = 692,
            Info = (current ? "● affichée en ce moment   " : "") + $"{sources.Count} source{(sources.Count > 1 ? "s" : "")}", InfoAccent = current,
            Nicknames = NicknamesOf(scene).ToList(),
            Note = bad.Contains(scene) || Normalizer.Normalize(scene).Length == 0 ? "⚠ Régis ne peut pas entendre ce nom : ajoute un surnom" : "Pas de surnom : dis simplement le nom",
        };
        Wire(header, scene);
        _headers[scene] = (header, sources.Count);
        header.ToggleClicked += () => ToggleGroup(scene, group, header);
        WireDrag(header, group, order => { _settings.SceneOrder = order.ToList(); _save(); _signature = Signature(); }, on => group.Highlight = on);
        group.Stack.Controls.Add(header);
        if (expanded) group.Stack.Controls.Add(BuildRows(scene, sources, NameRow.RowKind.Source, bad));
        return group;
    }

    // Les lignes de sources d'une scène (ou des sons), dans l'ordre choisi par l'utilisateur.
    private FlowLayoutPanel BuildRows(string orderKey, IReadOnlyList<string> names, NameRow.RowKind kind, HashSet<string> bad)
    {
        var rows = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Tag = "rows", Margin = Padding.Empty, Width = 692 };
        var ordered = OrderHelper.Apply(names, _settings.SourceOrder.GetValueOrDefault(orderKey));
        var seen = new HashSet<string>();
        foreach (var name in ordered)
        {
            var duplicate = !seen.Add(Normalizer.Normalize(name));
            var unspeakable = bad.Contains(name) || Normalizer.Normalize(name).Length == 0;
            var row = new NameRow
            {
                Kind = kind, Title = name, Height = 52, Width = 692, Margin = Padding.Empty, Separator = true, Tag = name,
                Nicknames = NicknamesOf(name).ToList(),
                Note = unspeakable ? "⚠ Régis ne peut pas entendre ce nom → surnom" : duplicate ? "⚠ Nom en double : Régis utilisera le premier" : null,
            };
            Wire(row, name);
            if (kind == NameRow.RowKind.Audio)
            {
                row.MuteClicked += async () =>
                {
                    try { _muted[name] = await _o.ToggleMuteAsync(name); }
                    catch (Exception) { /* OBS fermé : on ne change rien */ }
                    UpdateMuteButtons();
                };
                if (!_muteRows.TryGetValue(name, out var list)) _muteRows[name] = list = new List<NameRow>();
                list.Add(row);
            }
            WireDrag(row, row, order => { _settings.SourceOrder[orderKey] = order.ToList(); _save(); _signature = Signature(); }, on => row.Dragging = on);
            rows.Controls.Add(row);
        }
        return rows;
    }

    // Les pastilles et le bouton « + Surnom » d'une ligne.
    private void Wire(NameRow row, string name)
    {
        row.ChipRemoved += nick => { _settings.Nicknames.Remove(nick); _nicknamesChanged(); };
        row.AddClicked += () => _editor.Open(this, row, name, text => ValidateNickname(text, name), text =>
        {
            _settings.Nicknames[text] = name;
            _nicknamesChanged();
        });
    }

    private static void WireDrag(NameRow row, Control item, Action<IReadOnlyList<string>> dropped, Action<bool> visual)
    {
        DragReorder.Session? session = null;
        row.DragStarted += () => { session = new DragReorder.Session(item, dropped); visual(true); };
        row.DragMoved += () => session?.Move();
        row.DragEnded += () => { session?.End(); session = null; visual(false); };
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

    // ---- muet : mis à jour sur place, sans tout reconstruire ----

    public void UpdateMuteButtons()
    {
        foreach (var (name, rows) in _muteRows)
        {
            var muted = _muted.TryGetValue(name, out var m) && m;
            foreach (var r in rows) r.Muted = muted;
        }
    }
}

/// <summary>Le champ « Écris un surnom… » partagé : une seule fenêtre flottante réutilisée pour toutes les lignes.</summary>
public class NicknameEditor : Card
{
    private readonly TextBox _box = new() { PlaceholderText = "Écris un surnom…", Location = new Point(14, 14), Size = new Size(190, 28) };
    private readonly Button _add = new() { Text = "Ajouter", Tag = "primary", Location = new Point(212, 12), Size = new Size(84, 32) };
    private readonly Button _cancel = new() { Text = "✕", Location = new Point(302, 12), Size = new Size(34, 32) };
    private readonly Label _error = new() { Tag = "danger", Font = Theme.SmallFont, Location = new Point(14, 48), Size = new Size(322, 18) };
    private Func<string, string?>? _validate;
    private Action<string>? _commit;

    public NicknameEditor()
    {
        Size = new Size(350, 74); Visible = false; Highlight = true;
        Controls.AddRange(new Control[] { _box, _add, _cancel, _error });
        _add.Click += (_, _) => Submit();
        _cancel.Click += (_, _) => Close();
        _box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Submit(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
        };
        _box.TextChanged += (_, _) => _error.Text = "";
    }

    /// <summary>Ouvre l'éditeur à droite de la ligne donnée.</summary>
    public void Open(Control host, NameRow row, string realName, Func<string, string?> validate, Action<string> commit)
    {
        _validate = validate; _commit = commit;
        _box.Text = ""; _error.Text = "";
        _box.PlaceholderText = $"Surnom pour « {realName} »";
        var right = host.PointToClient(row.PointToScreen(new Point(row.Width - 10, 0))).X;
        var top = host.PointToClient(row.PointToScreen(new Point(0, row.Height / 2))).Y - Height / 2;
        Location = new Point(Math.Max(0, right - Width), Math.Clamp(top, 0, Math.Max(0, host.Height - Height)));
        Theme.Apply(this);
        Visible = true; BringToFront(); _box.Focus();
    }

    public void Close() { if (Visible) Visible = false; }

    private void Submit()
    {
        var text = _box.Text.Trim();
        var problem = text.Length == 0 ? "Écris d'abord un mot." : _validate?.Invoke(text);
        if (problem != null) { _error.Text = problem; return; }
        var commit = _commit;
        Close();
        commit?.Invoke(text);
    }
}
