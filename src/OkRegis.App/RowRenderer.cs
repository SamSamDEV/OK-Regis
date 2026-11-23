using OkRegis.Core.Localization;

namespace OkRegis.App;

public enum RowKind { Scene, Source, Audio }
public enum HitKind { None, Handle, Title, Mute, Plus, ChipCross }

/// <summary>Ce qui est survolé dans une ligne : quelle zone, et quelle pastille (pour la croix).</summary>
public readonly record struct Hover(HitKind Kind, int Chip)
{
    public static readonly Hover None = new(HitKind.None, -1);
}

/// <summary>Les données d'une ligne (scène, source ou son) : tout ce qu'il faut pour la dessiner.</summary>
public sealed class RowData
{
    public RowKind Kind { get; init; }
    /// <summary>Le vrai nom dans OBS.</summary>
    public string Name { get; init; } = "";
    public string Info { get; set; } = "";
    public bool InfoAccent { get; set; }
    public IReadOnlyList<string> Nicknames { get; init; } = Array.Empty<string>();
    public string? Note { get; init; }
    public bool Muted { get; set; }
    public bool Expanded { get; set; }
    public bool Separator { get; init; }

    // mise en page mémorisée (recalculée seulement si la largeur change)
    internal RowLayout? Cached;
    internal int CachedWidth, CachedHeight;
    internal string? CachedInfo;

    internal RowLayout GetLayout(int width, int height)
    {
        if (Cached == null || CachedWidth != width || CachedHeight != height || CachedInfo != Info)
        {
            Cached = RowRenderer.Layout(this, width, height);
            CachedWidth = width; CachedHeight = height; CachedInfo = Info;
        }
        return Cached;
    }
}

/// <summary>Les zones d'une ligne (en coordonnées relatives à la ligne).</summary>
internal sealed class RowLayout
{
    public Rectangle Handle, Title, Mute, Plus, ChipsArea;
    public List<(Rectangle Rect, string Nick)> Chips { get; } = new();
}

/// <summary>
/// Dessine et teste une ligne d'un seul bloc : poignée, nom, pastilles de surnoms, bouton muet, « + Surnom ».
/// Aucun contrôle Windows : c'est ce qui rend l'affichage fluide, même avec des centaines de sources.
/// </summary>
public static class RowRenderer
{
    private const int ChipHeight = 26, CrossWidth = 22, PlusWidth = 108, PillHeight = 30;
    private static readonly Size Huge = new(int.MaxValue, 100);

    // ---- mise en page ----

    internal static RowLayout Layout(RowData d, int width, int height)
    {
        var scene = d.Kind == RowKind.Scene;
        var midY = height / 2;
        var l = new RowLayout { Handle = new Rectangle(scene ? 6 : 28, 0, 28, height) };

        var x = 62;
        int chipsX;
        if (scene)
        {
            var titleWidth = TextRenderer.MeasureText(d.Name, Theme.CardTitleFont, Huge, TextFormatFlags.NoPadding).Width;
            var infoWidth = d.Info.Length > 0 ? TextRenderer.MeasureText(d.Info, Theme.SmallFont, Huge, TextFormatFlags.NoPadding).Width + 14 : 0;
            l.Title = new Rectangle(x, 0, Math.Min(titleWidth, Math.Max(80, width / 3)), height);
            chipsX = l.Title.Right + infoWidth + 18;
        }
        else
        {
            var nameWidth = d.Kind == RowKind.Audio ? 150 : 190;
            l.Title = new Rectangle(x, 0, nameWidth, height);
            chipsX = l.Title.Right + 10;
            if (d.Kind == RowKind.Audio) { l.Mute = new Rectangle(chipsX, midY - PillHeight / 2, 104, PillHeight); chipsX = l.Mute.Right + 10; }
        }
        l.Plus = new Rectangle(width - 14 - PlusWidth, midY - PillHeight / 2, PlusWidth, PillHeight);
        l.ChipsArea = new Rectangle(chipsX, 0, Math.Max(0, l.Plus.X - 10 - chipsX), height);

        var cx = l.ChipsArea.X;
        foreach (var nick in d.Nicknames)
        {
            var w = TextRenderer.MeasureText(nick, Theme.BaseFont, Huge, TextFormatFlags.NoPadding).Width + 14 + CrossWidth;
            if (cx + w > l.ChipsArea.Right) break; // trop de pastilles pour la place : les suivantes sont cachées
            l.Chips.Add((new Rectangle(cx, midY - ChipHeight / 2, w, ChipHeight), nick));
            cx += w + 6;
        }
        return l;
    }

    /// <summary>Le rectangle du bouton « + Surnom » (coordonnées relatives à la ligne).</summary>
    public static Rectangle PlusRect(RowData d, int width, int height) => d.GetLayout(width, height).Plus;

    // ---- dessin ----

    /// <param name="bounds">où dessiner la ligne (coordonnées de la surface)</param>
    public static void Paint(Graphics g, RowData d, Rectangle bounds, Hover hover, bool lifted)
    {
        var l = d.GetLayout(bounds.Width, bounds.Height);
        var o = bounds.Location;
        Rectangle R(Rectangle r) => new(r.X + o.X, r.Y + o.Y, r.Width, r.Height);
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;
        const TextFormatFlags center = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;
        var scene = d.Kind == RowKind.Scene;

        if (lifted) { using var b = new SolidBrush(Theme.SurfaceAlt); g.FillRectangle(b, bounds); }
        if (d.Separator) { using var pen = new Pen(Theme.Border); g.DrawLine(pen, bounds.X + 12, bounds.Y, bounds.Right - 12, bounds.Y); }

        TextRenderer.DrawText(g, "⠿", Theme.SymbolFont14, R(l.Handle), hover.Kind == HitKind.Handle ? Theme.Accent : Theme.Muted, center);

        if (scene)
        {
            TextRenderer.DrawText(g, d.Expanded ? "▾" : "▸", Theme.SymbolFont12, R(new Rectangle(34, 0, 24, bounds.Height)), Theme.Muted, center);
            TextRenderer.DrawText(g, d.Name.Length > 0 ? d.Name : Loc.T("(nom vide)"), Theme.CardTitleFont, R(l.Title), Theme.Text, flags);
            if (d.Info.Length > 0)
                TextRenderer.DrawText(g, d.Info, Theme.SmallFont,
                    R(new Rectangle(l.Title.Right + 8, 0, Math.Max(0, l.ChipsArea.X - l.Title.Right - 12), bounds.Height)),
                    d.InfoAccent ? Theme.Accent : Theme.Muted, flags);
        }
        else
        {
            TextRenderer.DrawText(g, d.Name.Length > 0 ? d.Name : Loc.T("(nom vide)"), Theme.BaseFont, R(l.Title), Theme.Text, flags);
        }

        if (d.Kind == RowKind.Audio)
            DrawPill(g, R(l.Mute), d.Muted ? Loc.T("🔇  Coupé") : Loc.T("🔊  Actif"), d.Muted ? Theme.Danger : Theme.SurfaceAlt, d.Muted ? Color.White : Theme.Text, hover.Kind == HitKind.Mute);

        for (var i = 0; i < l.Chips.Count; i++)
        {
            var (rect, nick) = l.Chips[i];
            var r = R(rect);
            using (var path = Theme.RoundedRect(r, 13)) using (var fill = new SolidBrush(Theme.ChipBack)) g.FillPath(fill, path);
            TextRenderer.DrawText(g, nick, Theme.BaseFont, new Rectangle(r.X + 10, r.Y, r.Width - CrossWidth - 10, r.Height), Theme.Accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
            var crossColor = hover.Kind == HitKind.ChipCross && hover.Chip == i ? Theme.Danger : Theme.Muted;
            TextRenderer.DrawText(g, "✕", Theme.SmallFont, new Rectangle(r.Right - CrossWidth, r.Y, CrossWidth - 4, r.Height), crossColor, center);
        }
        if (d.Nicknames.Count == 0 && !string.IsNullOrEmpty(d.Note))
            TextRenderer.DrawText(g, d.Note, Theme.SmallFont, R(l.ChipsArea), d.Note.StartsWith("⚠") ? Theme.Danger : Theme.Muted, flags);

        DrawPill(g, R(l.Plus), Loc.T("+  Surnom"), hover.Kind == HitKind.Plus ? ControlPaint.Light(Theme.Accent, 0.15f) : Theme.Accent, Theme.AccentText, false);
    }

    private static void DrawPill(Graphics g, Rectangle rect, string text, Color back, Color fore, bool hover)
    {
        using (var path = Theme.RoundedRect(rect, 8))
        using (var fill = new SolidBrush(hover ? ControlPaint.Light(back, 0.1f) : back)) g.FillPath(fill, path);
        TextRenderer.DrawText(g, text, Theme.BaseFont, rect, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping);
    }

    // ---- souris ----

    /// <param name="local">le point, relatif au coin haut-gauche de la ligne</param>
    public static Hover HitTest(RowData d, Point local, int width, int height)
    {
        var l = d.GetLayout(width, height);
        if (l.Plus.Contains(local)) return new Hover(HitKind.Plus, -1);
        if (d.Kind == RowKind.Audio && l.Mute.Contains(local)) return new Hover(HitKind.Mute, -1);
        for (var i = 0; i < l.Chips.Count; i++)
            if (l.Chips[i].Rect.Contains(local) && local.X >= l.Chips[i].Rect.Right - CrossWidth) return new Hover(HitKind.ChipCross, i);
        if (l.Handle.Contains(local)) return new Hover(HitKind.Handle, -1);
        if (d.Kind == RowKind.Scene && new Rectangle(34, 0, Math.Max(0, l.ChipsArea.X - 34), height).Contains(local)) return new Hover(HitKind.Title, -1);
        return Hover.None;
    }

    /// <summary>Le surnom de la pastille d'indice i (pour la retirer).</summary>
    public static string? NicknameAt(RowData d, int chip, int width, int height)
    {
        var l = d.GetLayout(width, height);
        return chip >= 0 && chip < l.Chips.Count ? l.Chips[chip].Nick : null;
    }
}
