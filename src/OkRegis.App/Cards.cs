namespace OkRegis.App;

/// <summary>Une carte : fond arrondi avec fine bordure, qui suit le thème.</summary>
public class Card : Panel
{
    /// <summary>Vrai pendant qu'on déplace la carte : bordure d'accent.</summary>
    public bool Highlight { get => _highlight; set { _highlight = value; Invalidate(); } }
    private bool _highlight;

    public Card()
    {
        DoubleBuffered = true; ResizeRedraw = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 12);
        using var fill = new SolidBrush(Theme.Surface);
        using var pen = new Pen(_highlight ? Theme.Accent : Theme.Border, _highlight ? 2f : 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Une pastille de surnom avec une croix pour la retirer.</summary>
public class Chip : Control
{
    private const int CrossWidth = 22;
    public event Action? Removed;

    public Chip(string text)
    {
        Text = text; Font = Theme.BaseFont; Cursor = Cursors.Default; Margin = new Padding(0, 0, 6, 0);
        DoubleBuffered = true;
        BackColor = Theme.Surface; // fond opaque : bien plus rapide à redessiner qu'un fond transparent
        var textSize = TextRenderer.MeasureText(text, Font);
        Size = new Size(textSize.Width + 14 + CrossWidth, 28);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 13))
        using (var fill = new SolidBrush(Theme.ChipBack))
            e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(10, 0, Width - CrossWidth - 10, Height), Theme.Accent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, "✕", Theme.SmallFont, new Rectangle(Width - CrossWidth, 0, CrossWidth - 4, Height), Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = e.X >= Width - CrossWidth ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.X >= Width - CrossWidth) Removed?.Invoke();
    }
}

/// <summary>Une colonne de cartes qui défile et s'adapte à la largeur de la fenêtre.</summary>
public class CardList : FlowLayoutPanel
{
    public CardList()
    {
        FlowDirection = FlowDirection.TopDown; WrapContents = false; AutoScroll = true;
        Padding = new Padding(4, 8, 4, 8); DoubleBuffered = true;
    }

    public void SetItems(IEnumerable<Control> items)
    {
        SuspendLayout();
        var scroll = VerticalScroll.Value; // on garde la position de défilement
        foreach (var old in Controls.Cast<Control>().ToList()) { Controls.Remove(old); old.Dispose(); }
        foreach (var item in items) { item.Margin = new Padding(0, 0, 0, 10); Controls.Add(item); }
        FitWidths();
        ResumeLayout(true);
        if (scroll > 0) AutoScrollPosition = new Point(0, scroll);
    }

    // Empêche la liste de sauter quand un champ prend le focus (elle restait défilée vers le bas).
    protected override Point ScrollToControl(Control activeControl) => DisplayRectangle.Location;

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        FitWidths();
    }

    private void FitWidths()
    {
        var width = Math.Max(300, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (Control c in Controls) c.Width = width;
    }
}
