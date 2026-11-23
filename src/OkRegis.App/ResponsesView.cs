using OkRegis.Core.Commands;
using OkRegis.Core.Languages;
using OkRegis.Core.Localization;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>Une action pour laquelle Régis répond : sa phrase et, éventuellement, un fichier son choisi.</summary>
public sealed class ResponseItem
{
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Phrase { get; set; } = "";
    public string? File { get; set; }
}

/// <summary>Les cartes « Quand je change de scène… », dessinées sur une seule surface fluide (sans contrôle par carte).</summary>
public class ResponsesCanvas : SmoothCanvas
{
    public const int CardH = 118;
    private const int Margin = 12, Gap = 10;
    private enum Zone { None, Phrase, Listen, Choose, Remove }

    private readonly List<ResponseItem> _items = new();
    private int _hoverIndex = -1; private Zone _hoverZone;
    private int _downIndex = -1; private Zone _downZone;

    public event Action<ResponseItem, Rectangle>? PhraseClicked;   // la carte et le rectangle de sa phrase (dans la surface)
    public event Action<ResponseItem>? ListenClicked, ChooseClicked, RemoveClicked;

    public void SetItems(IEnumerable<ResponseItem> items)
    {
        _items.Clear(); _items.AddRange(items);
        ContentHeight = Margin * 2 + _items.Count * CardH + Math.Max(0, _items.Count - 1) * Gap;
        SyncScroll();
        Invalidate();
    }

    private int CardWidth => Math.Max(300, ClientSize.Width - 2 * Margin);
    private Rectangle CardRect(int i) => new(Margin, Margin + i * (CardH + Gap) - Scroll, CardWidth, CardH);

    // Les zones d'une carte (dans la surface).
    private Rectangle PhraseRect(Rectangle c) => new(c.X + 18, c.Y + 44, c.Width - 36, 28);
    private static Rectangle ListenRect(Rectangle c) => new(c.X + 18, c.Y + 78, 110, 32);
    private static Rectangle ChooseRect(Rectangle c) => new(c.X + 136, c.Y + 78, 150, 32);
    private static Rectangle RemoveRect(Rectangle c) => new(c.Right - 50, c.Y + 78, 34, 32);
    private static Rectangle FileRect(Rectangle c) => new(c.X + 296, c.Y + 84, Math.Max(0, c.Width - 296 - 60), 22);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bg);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        for (var i = 0; i < _items.Count; i++)
        {
            var c = CardRect(i);
            if (c.Bottom < 0 || c.Top > ClientSize.Height) continue; // hors de l'écran
            PaintCard(g, _items[i], c, i == _hoverIndex ? _hoverZone : Zone.None);
        }
    }

    private void PaintCard(Graphics g, ResponseItem item, Rectangle c, Zone hover)
    {
        using (var path = Theme.RoundedRect(new Rectangle(c.X, c.Y, c.Width - 1, c.Height - 1), 12))
        using (var fill = new SolidBrush(Theme.Surface)) using (var pen = new Pen(Theme.Border))
        { g.FillPath(fill, path); g.DrawPath(pen, path); }

        const TextFormatFlags left = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;
        TextRenderer.DrawText(g, item.Title, Theme.CardTitleFont, new Rectangle(c.X + 18, c.Y + 10, c.Width - 36, 26), Theme.Text, left);

        // la « boîte » de la phrase
        var pr = PhraseRect(c);
        using (var path = Theme.RoundedRect(pr, 6))
        using (var fill = new SolidBrush(Theme.SurfaceAlt)) using (var pen = new Pen(hover == Zone.Phrase ? Theme.Accent : Theme.Border))
        { g.FillPath(fill, path); g.DrawPath(pen, path); }
        var empty = item.Phrase.Length == 0;
        TextRenderer.DrawText(g, empty ? Loc.T("Phrase que Régis dit…") : item.Phrase, Theme.BaseFont,
            new Rectangle(pr.X + 8, pr.Y, pr.Width - 16, pr.Height), empty ? Theme.Muted : Theme.Text, left);

        Pill(g, ListenRect(c), Loc.T("▶  Écouter"), hover == Zone.Listen);
        Pill(g, ChooseRect(c), item.File != null ? Loc.T("Changer le son…") : Loc.T("Choisir un son…"), hover == Zone.Choose);
        TextRenderer.DrawText(g, item.File != null ? "♪  " + Path.GetFileName(item.File) : Loc.T("Aucun son : Régis utilise sa voix"),
            Theme.BaseFont, FileRect(c), item.File != null ? Theme.Accent : Theme.Muted, left);
        if (item.File != null) Pill(g, RemoveRect(c), "✕", hover == Zone.Remove);
    }

    private static void Pill(Graphics g, Rectangle r, string text, bool hover)
    {
        using (var path = Theme.RoundedRect(r, 8)) using (var fill = new SolidBrush(hover ? Theme.Border : Theme.SurfaceAlt)) g.FillPath(fill, path);
        TextRenderer.DrawText(g, text, Theme.BaseFont, r, Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    // ---- souris ----

    private (int Index, Zone Zone) HitAt(Point p)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var c = CardRect(i);
            if (!c.Contains(p)) continue;
            if (_items[i].File != null && RemoveRect(c).Contains(p)) return (i, Zone.Remove);
            if (ListenRect(c).Contains(p)) return (i, Zone.Listen);
            if (ChooseRect(c).Contains(p)) return (i, Zone.Choose);
            if (PhraseRect(c).Contains(p)) return (i, Zone.Phrase);
            return (i, Zone.None);
        }
        return (-1, Zone.None);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (i, z) = HitAt(e.Location);
        if (i != _hoverIndex || z != _hoverZone) { _hoverIndex = i; _hoverZone = z; Invalidate(); }
        Cursor = z == Zone.Phrase ? Cursors.IBeam : z == Zone.None ? Cursors.Default : Cursors.Hand;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1) { _hoverIndex = -1; _hoverZone = Zone.None; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left) (_downIndex, _downZone) = HitAt(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var (i, z) = HitAt(e.Location);
        if (i < 0 || i != _downIndex || z != _downZone || z == Zone.None) return;
        var item = _items[i]; var c = CardRect(i);
        switch (z)
        {
            case Zone.Phrase: PhraseClicked?.Invoke(item, PhraseRect(c)); break;
            case Zone.Listen: ListenClicked?.Invoke(item); break;
            case Zone.Choose: ChooseClicked?.Invoke(item); break;
            case Zone.Remove: RemoveClicked?.Invoke(item); break;
        }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}

/// <summary>L'onglet « Réponses de Régis » : le mode de réponse en haut, puis une carte par action.</summary>
public class ResponsesView : Panel
{
    // Les actions pour lesquelles Régis répond (clé = clé dans AppSettings.Responses).
    private static readonly (string Key, string Title)[] ActionRows =
    {
        ("scene", Loc.T("Quand je change de scène")), ("show", Loc.T("Quand j'affiche une source")), ("hide", Loc.T("Quand je masque une source")),
        ("mute", Loc.T("Quand je coupe un son")), ("unmute", Loc.T("Quand je rétablis un son")),
        ("volumeUp", Loc.T("Quand je monte le volume")), ("volumeDown", Loc.T("Quand je baisse le volume")),
        ("recordStart", Loc.T("Quand je lance l'enregistrement")), ("recordStop", Loc.T("Quand j'arrête l'enregistrement")),
        ("recordPause", Loc.T("Quand je mets l'enregistrement en pause")),
        ("streamStart", Loc.T("Quand je lance le stream")), ("streamStop", Loc.T("Quand j'arrête le stream")),
        ("failed", Loc.T("Quand ça n'a pas marché")),
    };
    private static readonly string[] ModeLabels = { Loc.T("Voix de Régis (ou mes sons)"), Loc.T("Juste un bip"), Loc.T("Silence") };

    private readonly AppSettings _settings;
    private readonly Action _save;
    private readonly Func<string, string, Task> _preview;
    private readonly ResponsesCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly TextBox _editor = new() { Visible = false, BorderStyle = BorderStyle.None };
    private ResponseItem? _editing;
    private List<ResponseItem> _items = new();

    public ResponsesView(AppSettings settings, Action save, Func<string, string, Task> preview)
    {
        _settings = settings; _save = save; _preview = preview;

        var topHost = new Panel { Dock = DockStyle.Top, Height = 150, Padding = new Padding(12, 12, 12, 0) };
        var top = new Card { Dock = DockStyle.Fill };
        topHost.Controls.Add(top);
        top.Controls.Add(new Label { Text = Loc.T("Comment Régis répond ?"), Font = Theme.CardTitleFont, AutoSize = true, Location = new Point(18, 12) });
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
            Text = Loc.T("Écris la phrase que Régis dit ({scène} et {source} sont remplacés par le nom). Si tu choisis un fichier son, il passe avant la voix."),
            Tag = "muted", Font = Theme.SmallFont, AutoSize = false, Location = new Point(18, 80), Size = new Size(640, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        });

        Controls.Add(_editor); Controls.Add(_canvas); Controls.Add(topHost);
        _editor.BringToFront();

        _canvas.PhraseClicked += (item, rect) => OpenEditor(item, rect);
        _canvas.ListenClicked += async item => { Commit(item, item.Phrase); await _preview(item.Key, new ResponsePicker(_settings).Pick(item.Key, "LoL")); };
        _canvas.ChooseClicked += item =>
        {
            using var dialog = new OpenFileDialog { Title = Loc.T("Choisir un son"), Filter = Loc.T("Sons (*.wav;*.mp3)|*.wav;*.mp3") };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            item.File = dialog.FileName; _settings.CustomResponseFiles[item.Key] = dialog.FileName; _save(); _canvas.Invalidate();
        };
        _canvas.RemoveClicked += item => { item.File = null; _settings.CustomResponseFiles.Remove(item.Key); _save(); _canvas.Invalidate(); };
        _canvas.Scrolled += CloseEditor;

        _editor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CloseEditor(); }
            else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; _editing = null; _editor.Visible = false; _canvas.Invalidate(); }
        };
        _editor.Leave += (_, _) => CloseEditor();

        Build();
    }

    private void Build()
    {
        var picker = new ResponsePicker(_settings);
        _items = ActionRows.Select(a => new ResponseItem
        {
            Key = a.Key, Title = a.Title,
            Phrase = string.Join(" | ", picker.PhrasesFor(a.Key)), // la phrase personnalisée, sinon celle par défaut de la langue
            File = _settings.CustomResponseFiles.TryGetValue(a.Key, out var f) && f.Length > 0 ? f : null,
        }).ToList();
        _canvas.SetItems(_items);
    }

    // Le champ de saisie partagé se pose sur la phrase cliquée.
    private void OpenEditor(ResponseItem item, Rectangle rect)
    {
        CloseEditor();
        _editing = item;
        _editor.BackColor = Theme.SurfaceAlt; _editor.ForeColor = Theme.Text; _editor.Font = Theme.BaseFont;
        _editor.Text = item.Phrase;
        _editor.SetBounds(rect.X + 8, rect.Y + 5, rect.Width - 16, rect.Height - 10);
        _editor.Visible = true; _editor.BringToFront(); _editor.Focus(); _editor.SelectionStart = _editor.Text.Length;
    }

    private void CloseEditor()
    {
        if (_editing == null) return;
        var item = _editing; _editing = null;
        var text = _editor.Text;
        _editor.Visible = false;
        Commit(item, text);
        _canvas.Invalidate();
    }

    private void Commit(ResponseItem item, string text)
    {
        item.Phrase = text;
        var phrases = text.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        var defaults = LanguagePack.For(_settings.Language).DefaultResponses.GetValueOrDefault(item.Key) ?? Array.Empty<string>();
        // Identique à la phrase par défaut (ou vide) : on ne garde rien, la langue choisie fournit la phrase.
        if (phrases.Count == 0 || phrases.SequenceEqual(defaults)) _settings.Responses.Remove(item.Key); else _settings.Responses[item.Key] = phrases;
        _save();
    }
}
