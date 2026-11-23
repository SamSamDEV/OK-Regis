namespace OkRegis.App;

/// <summary>
/// Une ligne (scène, source ou son) dessinée d'un seul bloc : poignée, nom, pastilles de surnoms, bouton muet, « + Surnom ».
/// Un seul contrôle Windows par ligne au lieu d'une demi-douzaine : le défilement reste fluide même avec beaucoup de sources.
/// </summary>
public class NameRow : Control
{
    public enum RowKind { Scene, Source, Audio }
    private enum HitKind { None, Handle, Title, Mute, Plus, ChipCross }

    private const int ChipHeight = 26, CrossWidth = 22, PlusWidth = 108, PillHeight = 30;

    // ---- données affichées ----
    public RowKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Info { get; set; } = "";
    public bool InfoAccent { get; set; }
    public IReadOnlyList<string> Nicknames { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
    public bool Expanded { get; set; }
    public bool Muted { get => _muted; set { _muted = value; Invalidate(); } }
    public bool Separator { get; set; }
    public bool Dragging { get => _dragging; set { _dragging = value; BackColor = value ? Theme.SurfaceAlt : Theme.Surface; Invalidate(); } }

    // ---- événements ----
    public event Action<string>? ChipRemoved;
    public event Action? AddClicked;
    public event Action? MuteClicked;
    public event Action? ToggleClicked;   // clic sur le nom / la flèche d'une scène
    public event Action? DragStarted, DragMoved, DragEnded;

    private bool _muted, _dragging, _active;
    private HitKind _hover, _down;
    private int _hoverChip = -1, _downChip = -1;
    private Rectangle _handle, _title, _mute, _plus;
    private readonly List<(Rectangle Rect, string Nick)> _chips = new();
    private Rectangle _chipsArea;

    public NameRow()
    {
        DoubleBuffered = true; ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Theme.Surface;
        Height = 52;
    }

    /// <summary>Le rectangle du bouton « + Surnom » (coordonnées du contrôle), pour placer l'éditeur.</summary>
    public Rectangle PlusRect { get { ComputeLayout(); return _plus; } }

    // ---- mise en page ----

    private void ComputeLayout()
    {
        var scene = Kind == RowKind.Scene;
        var midY = Height / 2;
        _handle = new Rectangle(scene ? 6 : 28, 0, 28, Height);

        var x = scene ? 62 : 62;
        var titleFont = scene ? Theme.CardTitleFont : Theme.BaseFont;
        var titleWidth = TextRenderer.MeasureText(Title, titleFont, new Size(int.MaxValue, 100), TextFormatFlags.NoPadding).Width;
        int chipsX;
        if (scene)
        {
            var infoWidth = Info.Length > 0 ? TextRenderer.MeasureText(Info, Theme.SmallFont, new Size(int.MaxValue, 100), TextFormatFlags.NoPadding).Width + 14 : 0;
            _title = new Rectangle(x, 0, Math.Min(titleWidth, Math.Max(80, Width / 3)), Height);
            chipsX = _title.Right + infoWidth + 18;
            _mute = Rectangle.Empty;
        }
        else
        {
            var nameWidth = Kind == RowKind.Audio ? 150 : 190;
            _title = new Rectangle(x, 0, nameWidth, Height);
            chipsX = _title.Right + 10;
            if (Kind == RowKind.Audio) { _mute = new Rectangle(chipsX, midY - PillHeight / 2, 104, PillHeight); chipsX = _mute.Right + 10; }
            else _mute = Rectangle.Empty;
        }
        _plus = new Rectangle(Width - 14 - PlusWidth, midY - PillHeight / 2, PlusWidth, PillHeight);
        _chipsArea = new Rectangle(chipsX, 0, Math.Max(0, _plus.X - 10 - chipsX), Height);

        _chips.Clear();
        var cx = _chipsArea.X;
        foreach (var nick in Nicknames)
        {
            var w = TextRenderer.MeasureText(nick, Theme.BaseFont, new Size(int.MaxValue, 100), TextFormatFlags.NoPadding).Width + 14 + CrossWidth;
            if (cx + w > _chipsArea.Right) break; // trop de pastilles pour la place : les suivantes sont cachées
            _chips.Add((new Rectangle(cx, midY - ChipHeight / 2, w, ChipHeight), nick));
            cx += w + 6;
        }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

    // ---- dessin ----

    protected override void OnPaint(PaintEventArgs e)
    {
        ComputeLayout();
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;
        var scene = Kind == RowKind.Scene;

        if (Separator) { using var pen = new Pen(Theme.Border); g.DrawLine(pen, 12, 0, Width - 12, 0); }

        TextRenderer.DrawText(g, "⠿", Theme.SymbolFont14, _handle, _hover == HitKind.Handle ? Theme.Accent : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);

        if (scene)
        {
            TextRenderer.DrawText(g, Expanded ? "▾" : "▸", Theme.SymbolFont12, new Rectangle(34, 0, 24, Height), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, Title.Length > 0 ? Title : "(nom vide)", Theme.CardTitleFont, _title, Theme.Text, flags);
            if (Info.Length > 0)
                TextRenderer.DrawText(g, Info, Theme.SmallFont, new Rectangle(_title.Right + 8, 0, Math.Max(0, _chipsArea.X - _title.Right - 12), Height), InfoAccent ? Theme.Accent : Theme.Muted, flags);
        }
        else
        {
            TextRenderer.DrawText(g, Title.Length > 0 ? Title : "(nom vide)", Theme.BaseFont, _title, Theme.Text, flags);
        }

        if (Kind == RowKind.Audio) DrawPill(g, _mute, _muted ? "🔇  Coupé" : "🔊  Actif", _muted ? Theme.Danger : Theme.SurfaceAlt, _muted ? Color.White : Theme.Text, _hover == HitKind.Mute);

        for (var i = 0; i < _chips.Count; i++)
        {
            var (rect, nick) = _chips[i];
            using (var path = Theme.RoundedRect(rect, 13)) using (var fill = new SolidBrush(Theme.ChipBack)) g.FillPath(fill, path);
            TextRenderer.DrawText(g, nick, Theme.BaseFont, new Rectangle(rect.X + 10, rect.Y, rect.Width - CrossWidth - 10, rect.Height), Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            var crossColor = _hover == HitKind.ChipCross && _hoverChip == i ? Theme.Danger : Theme.Muted;
            TextRenderer.DrawText(g, "✕", Theme.SmallFont, new Rectangle(rect.Right - CrossWidth, rect.Y, CrossWidth - 4, rect.Height), crossColor, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
        if (Nicknames.Count == 0 && !string.IsNullOrEmpty(Note))
            TextRenderer.DrawText(g, Note, Theme.SmallFont, _chipsArea, Note.StartsWith("⚠") ? Theme.Danger : Theme.Muted, flags);

        DrawPill(g, _plus, "+  Surnom", _hover == HitKind.Plus ? ControlPaint.Light(Theme.Accent, 0.15f) : Theme.Accent, Theme.AccentText, false);
    }

    private static void DrawPill(Graphics g, Rectangle rect, string text, Color back, Color fore, bool hover)
    {
        using (var path = Theme.RoundedRect(rect, 8))
        using (var fill = new SolidBrush(hover ? ControlPaint.Light(back, 0.1f) : back)) g.FillPath(fill, path);
        TextRenderer.DrawText(g, text, Theme.BaseFont, rect, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    // ---- souris ----

    private (HitKind Kind, int Chip) HitTest(Point p)
    {
        ComputeLayout();
        if (_plus.Contains(p)) return (HitKind.Plus, -1);
        if (Kind == RowKind.Audio && _mute.Contains(p)) return (HitKind.Mute, -1);
        for (var i = 0; i < _chips.Count; i++)
            if (_chips[i].Rect.Contains(p) && p.X >= _chips[i].Rect.Right - CrossWidth) return (HitKind.ChipCross, i);
        if (_handle.Contains(p)) return (HitKind.Handle, -1);
        if (Kind == RowKind.Scene && new Rectangle(34, 0, Math.Max(0, _chipsArea.X - 34), Height).Contains(p)) return (HitKind.Title, -1);
        return (HitKind.None, -1);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_active) { DragMoved?.Invoke(); return; }
        var (kind, chip) = HitTest(e.Location);
        if (kind != _hover || chip != _hoverChip) { _hover = kind; _hoverChip = chip; Invalidate(); }
        Cursor = kind switch { HitKind.Handle => Cursors.SizeAll, HitKind.None => Cursors.Default, _ => Cursors.Hand };
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != HitKind.None) { _hover = HitKind.None; _hoverChip = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        (_down, _downChip) = HitTest(e.Location);
        if (_down == HitKind.Handle) { _active = true; Capture = true; DragStarted?.Invoke(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_active) { _active = false; Capture = false; DragEnded?.Invoke(); return; }
        var (kind, chip) = HitTest(e.Location);
        if (kind != _down || chip != _downChip) return; // on a relâché ailleurs : pas un clic
        switch (kind)
        {
            case HitKind.Plus: AddClicked?.Invoke(); break;
            case HitKind.Mute: MuteClicked?.Invoke(); break;
            case HitKind.Title: ToggleClicked?.Invoke(); break;
            case HitKind.ChipCross when chip >= 0 && chip < _chips.Count: ChipRemoved?.Invoke(_chips[chip].Nick); break;
        }
    }
}
