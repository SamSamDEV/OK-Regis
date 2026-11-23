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
