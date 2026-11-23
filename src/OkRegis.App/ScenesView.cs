using OkRegis.Core.Commands;
using OkRegis.Core.Localization;
using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>
/// Les scènes comme dans OBS (une carte par scène avec ses sources dessous), ou la liste des sons (muet en un clic).
/// Le dessin et les animations sont dans SceneCanvas (une seule surface, fluide) ; ici on fabrique son contenu à partir
/// d'OBS et on branche les actions : surnoms, muet, ordre, dépliage.
/// </summary>
public class ScenesView : Panel
{
    private const string AudioKey = "(audio)";

    private readonly Orchestrator _o;
    private readonly AppSettings _settings;
    private readonly Vocabulary _vocab;
    private readonly Action _nicknamesChanged; // enregistre et reconstruit la grammaire (en arrière-plan)
    private readonly Action _save;             // enregistre seulement
    private readonly bool _audioOnly;          // vrai : cette vue n'affiche que les sons ; faux : que les scènes

    private readonly SceneCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly NicknameEditor _editor = new();
    private readonly HashSet<string> _expanded = new();   // scènes dépliées
    private readonly Dictionary<string, bool> _muted = new();
    private readonly Dictionary<string, RowData> _audioRows = new();
    private readonly Dictionary<string, (RowData Header, int Count)> _headers = new();
    private bool _expandInitialized;
    private string _signature = "";

    public ScenesView(Orchestrator o, AppSettings settings, Vocabulary vocab, Action nicknamesChanged, Action save, bool audioOnly = false)
    {
        _audioOnly = audioOnly;
        _o = o; _settings = settings; _vocab = vocab; _nicknamesChanged = nicknamesChanged; _save = save;
        Controls.Add(_editor); Controls.Add(_canvas);   // l'éditeur de surnom flotte au-dessus de la surface
        _editor.BringToFront();

        _canvas.Scrolled += () => _editor.Close();
        _canvas.ChipRemoved += (_, nick) => { _settings.Nicknames.Remove(nick); _nicknamesChanged(); };
        _canvas.AddClicked += (row, bounds) => _editor.Open(this, bounds, row.Name, text => ValidateNickname(text, row.Name), text =>
        {
            _settings.Nicknames[text] = row.Name;
            _nicknamesChanged();
        });
        _canvas.MuteClicked += async row =>
        {
            try { _muted[row.Name] = await _o.ToggleMuteAsync(row.Name); }
            catch (Exception) { /* OBS fermé : on ne change rien */ }
            UpdateMuteButtons();
        };
        _canvas.ExpandChanged += (scene, expanded) => { if (expanded) _expanded.Add(scene); else _expanded.Remove(scene); };
        _canvas.SceneOrderChanged += order => { _settings.SceneOrder = order.ToList(); _save(); _signature = Signature(); };
        _canvas.RowOrderChanged += (key, order) => { _settings.SourceOrder[key] = order.ToList(); _save(); _signature = Signature(); };
    }

    /// <summary>Le thème a changé : tout se redessine (les couleurs sont lues à chaque dessin).</summary>
    public void ThemeChanged() => _canvas.Redraw();

    /// <summary>Force la reconstruction au prochain RebuildIfChanged.</summary>
    public void ForceRebuild() => _signature = "";

    // On ne reconstruit que si le contenu change (sinon on perdrait la saisie et l'appli ramerait).
    public void RebuildIfChanged()
    {
        var signature = Signature();
        if (signature != _signature) { _signature = signature; Build(); }
        UpdateCurrentScene(); // changer de scène dans OBS ne reconstruit rien : on met juste à jour les en-têtes
    }

    private string Signature()
    {
        var snap = _o.Snapshot;
        if (snap is null) return "none";
        return string.Join("|", snap.Scenes) + "#" + string.Join("|", snap.AudioSources) + "#"
            + string.Join("|", snap.SourcesByScene?.Select(p => p.Key + ":" + string.Join(",", p.Value)) ?? Array.Empty<string>()) + "#"
            + string.Join("|", _settings.Nicknames.Select(n => n.Key + "=" + n.Value)) + "#"
            + string.Join("|", _o.Grammar?.Unspeakable ?? Array.Empty<string>()) + "#"
            + string.Join("|", _settings.SceneOrder) + "#" + string.Join("|", _settings.SourceOrder.Select(p => p.Key + ":" + string.Join(",", p.Value)));
    }

    // Met à jour « ● affichée en ce moment » sur place.
    private void UpdateCurrentScene()
    {
        var current = _o.Snapshot?.CurrentScene;
        var changed = false;
        foreach (var (scene, (header, count)) in _headers)
        {
            var isCurrent = scene == current;
            var info = (isCurrent ? Loc.T("● affichée en ce moment   ") : "") + Loc.T(count > 1 ? "{0} sources" : "{0} source", count);
            if (header.Info == info) continue;
            header.Info = info; header.InfoAccent = isCurrent; changed = true;
        }
        if (changed) _canvas.Redraw();
    }

    // ---- construction ----

    private void Build()
    {
        _editor.Close();
        var snap = _o.Snapshot;
        var groups = new List<GroupItem>();
        _audioRows.Clear(); _headers.Clear();

        if (snap is null)
        {
            // pas d'OBS : un simple message
            groups.Add(new GroupItem
            {
                Key = "message", Audio = true,
                Title = Loc.T(_audioOnly ? "Sons (audio)" : "Scènes"),
                Hint = Loc.T(_audioOnly
                    ? "Ouvre OBS : tes sons apparaîtront ici.\nTu pourras couper le micro ou Discord en un clic."
                    : "Ouvre OBS : tes scènes et leurs sources apparaîtront ici.\nTu pourras leur donner des surnoms.").Replace("\n", "  "),
            });
        }
        else
        {
            if (!_expandInitialized) { if (snap.CurrentScene != null) _expanded.Add(snap.CurrentScene); _expandInitialized = true; }
            _muted.Clear();
            foreach (var (name, isMuted) in snap.AudioMuted ?? new Dictionary<string, bool>()) _muted[name] = isMuted;

            var bad = new HashSet<string>(_o.Grammar?.Unspeakable ?? Array.Empty<string>());
            if (_audioOnly)
            {
                groups.Add(snap.AudioSources.Count > 0
                    ? BuildAudioGroup(snap, bad)
                    : new GroupItem { Key = "message", Audio = true, Title = Loc.T("Sons (audio)"), Hint = Loc.T("Aucune source audio trouvée dans OBS.") });
            }
            else
            {
                foreach (var scene in OrderHelper.Apply(snap.Scenes, _settings.SceneOrder)) groups.Add(BuildSceneGroup(scene, snap, bad));
            }
        }

        _canvas.SetModel(groups);
        UpdateMuteButtons();
    }

    private GroupItem BuildAudioGroup(ObsSnapshot snap, HashSet<string> bad)
    {
        var group = new GroupItem
        {
            Key = AudioKey, Audio = true, Title = Loc.T("Sons (audio)"),
            Hint = Loc.T("Coupe ou rétablis un son d'un clic. À la voix : « coupe le micro », « mute discord »…"),
        };
        AddRows(group, AudioKey, snap.AudioSources, RowKind.Audio, bad);
        return group;
    }

    private GroupItem BuildSceneGroup(string scene, ObsSnapshot snap, HashSet<string> bad)
    {
        var expanded = _expanded.Contains(scene);
        var sources = snap.SourcesByScene != null && snap.SourcesByScene.TryGetValue(scene, out var s) ? s : Array.Empty<string>();
        var current = scene == snap.CurrentScene;
        var header = new RowData
        {
            Kind = RowKind.Scene, Name = scene, Expanded = expanded,
            Info = (current ? Loc.T("● affichée en ce moment   ") : "") + Loc.T(sources.Count > 1 ? "{0} sources" : "{0} source", sources.Count),
            InfoAccent = current,
            Nicknames = NicknamesOf(scene).ToList(),
            Note = bad.Contains(scene) || Normalizer.Normalize(scene).Length == 0
                ? Loc.T("⚠ Régis ne peut pas entendre ce nom : ajoute un surnom") : Loc.T("Pas de surnom : dis simplement le nom"),
        };
        _headers[scene] = (header, sources.Count);
        var group = new GroupItem { Key = scene, Header = header, Expanded = expanded };
        AddRows(group, scene, sources, RowKind.Source, bad);
        return group;
    }

    // Les lignes de sources d'une scène (ou des sons), dans l'ordre choisi par l'utilisateur.
    private void AddRows(GroupItem group, string orderKey, IReadOnlyList<string> names, RowKind kind, HashSet<string> bad)
    {
        var seen = new HashSet<string>();
        foreach (var name in OrderHelper.Apply(names, _settings.SourceOrder.GetValueOrDefault(orderKey)))
        {
            var duplicate = !seen.Add(Normalizer.Normalize(name));
            var unspeakable = bad.Contains(name) || Normalizer.Normalize(name).Length == 0;
            var row = new RowData
            {
                Kind = kind, Name = name, Separator = true,
                Nicknames = NicknamesOf(name).ToList(),
                Note = unspeakable ? Loc.T("⚠ Régis ne peut pas entendre ce nom → surnom")
                     : duplicate ? Loc.T("⚠ Nom en double : Régis utilisera le premier") : null,
            };
            if (kind == RowKind.Audio) _audioRows[name] = row;
            group.Rows.Add(new RowItem { Data = row });
        }
    }

    private IEnumerable<string> NicknamesOf(string realName) =>
        _settings.Nicknames.Where(n => Normalizer.Normalize(n.Value) == Normalizer.Normalize(realName)).Select(n => n.Key);

    private string? ValidateNickname(string text, string realName)
    {
        if (!_vocab.CanSpeak(text, out _)) return Loc.T("Régis ne connaît pas « {0} ». Essaie un mot plus courant.", text);
        var key = Normalizer.Normalize(text);
        var snap = _o.Snapshot;
        var all = (snap?.Scenes ?? Array.Empty<string>()).Concat(snap?.AudioSources ?? Array.Empty<string>())
            .Concat(snap?.SourcesByScene?.Values.SelectMany(v => v) ?? Array.Empty<string>());
        if (all.Any(n => Normalizer.Normalize(n) == key && n != realName)) return Loc.T("Ce mot est déjà le nom d'une autre scène ou source.");
        if (_settings.Nicknames.TryGetValue(text, out var other) && Normalizer.Normalize(other) != Normalizer.Normalize(realName))
            return Loc.T("Ce surnom est déjà utilisé pour « {0} ».", other);
        return null;
    }

    // ---- muet : mis à jour sur place, sans tout reconstruire ----

    public void UpdateMuteButtons()
    {
        foreach (var (name, row) in _audioRows) row.Muted = _muted.TryGetValue(name, out var m) && m;
        _canvas.Redraw();
    }
}

/// <summary>Le champ « Écris un surnom… » partagé : une seule fenêtre flottante réutilisée pour toutes les lignes.</summary>
public class NicknameEditor : Card
{
    private readonly TextBox _box = new() { PlaceholderText = Loc.T("Écris un surnom…"), Location = new Point(14, 14), Size = new Size(190, 28) };
    private readonly Button _add = new() { Text = Loc.T("Ajouter"), Tag = "primary", Location = new Point(212, 12), Size = new Size(84, 32) };
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

    /// <summary>Ouvre l'éditeur à droite de la ligne donnée (rectangle en coordonnées de l'hôte).</summary>
    public void Open(Control host, Rectangle rowBounds, string realName, Func<string, string?> validate, Action<string> commit)
    {
        _validate = validate; _commit = commit;
        _box.Text = ""; _error.Text = "";
        _box.PlaceholderText = Loc.T("Surnom pour « {0} »", realName);
        var top = rowBounds.Y + rowBounds.Height / 2 - Height / 2;
        Location = new Point(Math.Max(0, rowBounds.Right - 10 - Width), Math.Clamp(top, 0, Math.Max(0, host.Height - Height)));
        Theme.Apply(this);
        Visible = true; BringToFront(); _box.Focus();
    }

    public void Close() { if (Visible) Visible = false; }

    private void Submit()
    {
        var text = _box.Text.Trim();
        var problem = text.Length == 0 ? Loc.T("Écris d'abord un mot.") : _validate?.Invoke(text);
        if (problem != null) { _error.Text = problem; return; }
        var commit = _commit;
        Close();
        commit?.Invoke(text);
    }
}
