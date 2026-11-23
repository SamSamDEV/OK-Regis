namespace OkRegis.App;

/// <summary>Une ligne dans un groupe, avec sa position animée (relative au haut des lignes du groupe).</summary>
public sealed class RowItem
{
    public RowData Data { get; init; } = null!;
    public string Key => Data.Name;
    public float DisplayY, TargetY;
    public bool Lifted;
}

/// <summary>Une carte : l'en-tête d'une scène (ou le titre du groupe « Sons ») et ses lignes.</summary>
public sealed class GroupItem
{
    public string Key { get; init; } = "";
    public bool Audio { get; init; }
    public RowData? Header { get; init; }
    public string Title { get; init; } = "";
    public string Hint { get; init; } = "";
    public List<RowItem> Rows { get; } = new();
    public bool Expanded { get; set; }
    public float ExpandT;                 // 0 = replié, 1 = déplié (animé)
    public float DisplayY, TargetY;
    public bool Lifted;
}

/// <summary>
/// La liste des scènes (ou des sons) dessinée sur UNE seule surface : aucun contrôle Windows par ligne.
/// Seul ce qui est visible est dessiné, donc le défilement, le dépliage et le glisser-déposer restent à 60 images/s
/// même avec des centaines de sources. Les animations (défilement adouci, dépliage, cartes qui glissent) ne tournent
/// que pendant qu'elles durent : au repos, rien ne s'exécute.
/// </summary>
public class SceneCanvas : SmoothCanvas
{
    private const int Margin = 12, Gap = 10, RowH = 52, SceneHeaderH = 60, AudioHeaderH = 58, Inset = 4;

    private readonly List<GroupItem> _groups = new();
    // survol
    private RowData? _hoverRow;
    private Hover _hover = Hover.None;
    // clic
    private RowData? _downRow; private Hover _down = Hover.None;
    // glisser
    private GroupItem? _dragGroup; private RowItem? _dragRow; private GroupItem? _dragRowGroup; private float _grab;

    public event Action<RowData, string>? ChipRemoved;
    public event Action<RowData, Rectangle>? AddClicked;     // la ligne et son rectangle dans la surface
    public event Action<RowData>? MuteClicked;
    public event Action<string, bool>? ExpandChanged;
    public event Action<IReadOnlyList<string>>? SceneOrderChanged;
    public event Action<string, IReadOnlyList<string>>? RowOrderChanged;
    protected override bool KeepRunning => _dragGroup != null || _dragRow != null;

    // ---- le contenu ----

    /// <summary>Remplace tout le contenu (les cartes apparaissent à leur place, sans animation).</summary>
    public void SetModel(IEnumerable<GroupItem> groups)
    {
        _groups.Clear(); _groups.AddRange(groups);
        foreach (var g in _groups) g.ExpandT = g.Audio || g.Expanded ? 1 : 0;
        Layout();
        foreach (var g in _groups) { g.DisplayY = g.TargetY; foreach (var r in g.Rows) r.DisplayY = r.TargetY; }
        _hoverRow = null; _hover = Hover.None;
        SyncScroll();
        Invalidate();
    }

    /// <summary>Redessine tout (changement de thème, bouton muet…).</summary>
    public void Redraw() => Invalidate();

    // ---- mise en page ----

    private int CardWidth => Math.Max(300, ClientSize.Width - 2 * Margin);
    private static int HeaderH(GroupItem g) => g.Audio ? AudioHeaderH : SceneHeaderH;
    private float RowsHeight(GroupItem g) => g.Rows.Count * RowH * g.ExpandT;
    private float GroupHeight(GroupItem g) => Inset + HeaderH(g) + RowsHeight(g) + Inset;

    // Positions « cibles » de tout le monde (les affichées les rejoignent en douceur).
    private void Layout()
    {
        float y = Margin;
        foreach (var g in _groups)
        {
            g.TargetY = y;
            y += GroupHeight(g) + Gap;
            for (var i = 0; i < g.Rows.Count; i++) g.Rows[i].TargetY = i * RowH;
        }
        ContentHeight = (int)Math.Ceiling(y - Gap + Margin);
    }

    // ---- animation ----

    // Fait avancer les animations de la carte (dépliage, glisser, cartes qui se décalent). Renvoie vrai s'il en reste.
    protected override bool StepContent(float dt)
    {
        var animating = false;

        // dépliage progressif
        foreach (var g in _groups)
        {
            if (g.Audio) continue;
            var target = g.Expanded ? 1f : 0f;
            if (MathF.Abs(g.ExpandT - target) > 0.003f) { g.ExpandT += (target - g.ExpandT) * (1 - MathF.Exp(-14 * dt)); animating = true; }
            else g.ExpandT = target;
        }

        // pendant un glisser : l'élément suit la souris, et on fait défiler si on approche des bords
        if (_dragGroup != null || _dragRow != null)
        {
            var y = PointToClient(Cursor.Position).Y;
            if (y < 50) ScrollTarget = Math.Max(0, ScrollTarget - 16);
            else if (y > ClientSize.Height - 50) ScrollTarget = Math.Min(MaxScroll, ScrollTarget + 16);
            UpdateDrag();
        }

        Layout();

        foreach (var g in _groups)
        {
            if (!g.Lifted) g.DisplayY = Ease(g.DisplayY, g.TargetY, 22, dt, ref animating);
            foreach (var r in g.Rows) if (!r.Lifted) r.DisplayY = Ease(r.DisplayY, r.TargetY, 24, dt, ref animating);
        }
        return animating;
    }

    // ---- dessin ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Bg);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var scroll = Scroll;
        GroupItem? lifted = null;
        foreach (var group in _groups)
        {
            if (group.Lifted) { lifted = group; continue; }
            var top = (int)Math.Round(group.DisplayY) - scroll;
            if (top > ClientSize.Height || top + (int)GroupHeight(group) < 0) continue; // hors de l'écran : on ne dessine pas
            PaintGroup(g, group, top);
        }
        if (lifted != null) PaintGroup(g, lifted, (int)Math.Round(lifted.DisplayY) - scroll); // la carte déplacée passe par-dessus
    }

    private void PaintGroup(Graphics g, GroupItem group, int top)
    {
        var w = CardWidth;
        var height = (int)Math.Round(GroupHeight(group));
        var card = new Rectangle(Margin, top, w, height);

        if (group.Lifted) // petite ombre sous la carte soulevée
            for (var i = 3; i >= 1; i--)
            {
                using var shadow = new SolidBrush(Color.FromArgb(22, 0, 0, 0));
                using var sp = Theme.RoundedRect(new Rectangle(card.X - i, card.Y + i * 2, card.Width + 2 * i, card.Height), 12 + i);
                g.FillPath(shadow, sp);
            }
        using (var path = Theme.RoundedRect(new Rectangle(card.X, card.Y, card.Width - 1, card.Height - 1), 12))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var pen = new Pen(group.Lifted ? Theme.Accent : Theme.Border, group.Lifted ? 2f : 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        var inner = new Rectangle(card.X + Inset, card.Y + Inset, card.Width - 2 * Inset, HeaderH(group));
        if (group.Audio)
        {
            TextRenderer.DrawText(g, group.Title, Theme.CardTitleFont, new Rectangle(inner.X + 14, inner.Y + 6, inner.Width - 28, 24), Theme.Text, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, group.Hint, Theme.SmallFont, new Rectangle(inner.X + 14, inner.Y + 32, inner.Width - 28, 18), Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
        else if (group.Header != null)
        {
            RowRenderer.Paint(g, group.Header, inner, group.Header == _hoverRow ? _hover : Hover.None, lifted: false);
        }

        if (group.ExpandT < 0.001f) return;
        var rowsArea = new Rectangle(inner.X, inner.Bottom, inner.Width, (int)Math.Ceiling(RowsHeight(group)));
        var saved = g.Clip;
        g.SetClip(rowsArea, System.Drawing.Drawing2D.CombineMode.Intersect);
        foreach (var row in group.Rows)
        {
            var rowTop = rowsArea.Y + (int)Math.Round(row.DisplayY);
            if (rowTop > ClientSize.Height || rowTop + RowH < 0) continue;
            RowRenderer.Paint(g, row.Data, new Rectangle(rowsArea.X, rowTop, rowsArea.Width, RowH), row.Data == _hoverRow ? _hover : Hover.None, row.Lifted);
        }
        g.Clip = saved;
    }

    // ---- souris ----

    // La ligne sous le curseur : son groupe, ses données, son rectangle (dans la surface) et la zone survolée.
    private bool HitAt(Point p, out GroupItem group, out RowItem? rowItem, out RowData data, out Rectangle bounds, out Hover hover)
    {
        var scroll = Scroll;
        var w = CardWidth;
        foreach (var g in _groups)
        {
            var top = (int)Math.Round(g.DisplayY) - scroll;
            var h = (int)Math.Round(GroupHeight(g));
            if (p.X < Margin || p.X >= Margin + w || p.Y < top || p.Y >= top + h) continue;
            group = g;
            var innerX = Margin + Inset; var innerW = w - 2 * Inset;
            var headerRect = new Rectangle(innerX, top + Inset, innerW, HeaderH(g));
            if (!g.Audio && g.Header != null && headerRect.Contains(p))
            {
                rowItem = null; data = g.Header; bounds = headerRect;
                hover = RowRenderer.HitTest(data, new Point(p.X - bounds.X, p.Y - bounds.Y), bounds.Width, bounds.Height);
                return true;
            }
            var rowsTop = headerRect.Bottom;
            foreach (var r in g.Rows)
            {
                var rb = new Rectangle(innerX, rowsTop + (int)Math.Round(r.DisplayY), innerW, RowH);
                if (rb.Contains(p) && p.Y < rowsTop + (int)RowsHeight(g))
                {
                    rowItem = r; data = r.Data; bounds = rb;
                    hover = RowRenderer.HitTest(data, new Point(p.X - rb.X, p.Y - rb.Y), rb.Width, rb.Height);
                    return true;
                }
            }
            break;
        }
        group = null!; rowItem = null; data = null!; bounds = Rectangle.Empty; hover = Hover.None;
        return false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragGroup != null || _dragRow != null) { UpdateDrag(); StartTimer(); return; }
        RowData? row = null; var hv = Hover.None;
        if (HitAt(e.Location, out _, out _, out var data, out _, out var hit)) { row = data; hv = hit; }
        if (row != _hoverRow || hv != _hover) { _hoverRow = row; _hover = hv; Invalidate(); }
        Cursor = hv.Kind switch { HitKind.Handle => Cursors.SizeAll, HitKind.None => Cursors.Default, _ => Cursors.Hand };
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverRow != null) { _hoverRow = null; _hover = Hover.None; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !HitAt(e.Location, out var group, out var rowItem, out var data, out var bounds, out var hit)) { _downRow = null; return; }
        _downRow = data; _down = hit;
        if (hit.Kind != HitKind.Handle) return;

        Capture = true;
        var contentY = e.Y + Scroll;
        if (rowItem == null) { _dragGroup = group; group.Lifted = true; _grab = contentY - group.DisplayY; }
        else { _dragRowGroup = group; _dragRow = rowItem; rowItem.Lifted = true; _grab = contentY - (group.DisplayY + Inset + HeaderH(group)) - rowItem.DisplayY; }
        StartTimer();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragGroup != null || _dragRow != null) { EndDrag(); return; }
        if (_downRow == null || !HitAt(e.Location, out var group, out _, out var data, out var bounds, out var hit)) { _downRow = null; return; }
        var sameSpot = data == _downRow && hit == _down;
        _downRow = null;
        if (!sameSpot) return;
        switch (hit.Kind)
        {
            case HitKind.Plus: AddClicked?.Invoke(data, bounds); break;
            case HitKind.Mute: MuteClicked?.Invoke(data); break;
            case HitKind.Title when !group.Audio: ToggleGroup(group); break;
            case HitKind.ChipCross:
                var nick = RowRenderer.NicknameAt(data, hit.Chip, bounds.Width, bounds.Height);
                if (nick != null) ChipRemoved?.Invoke(data, nick);
                break;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Layout();
        Invalidate();
    }

    // ---- déplier / replier ----

    private void ToggleGroup(GroupItem g)
    {
        g.Expanded = !g.Expanded;
        if (g.Header != null) g.Header.Expanded = g.Expanded;
        ExpandChanged?.Invoke(g.Key, g.Expanded);
        StartTimer();
    }

    // ---- glisser-déposer ----

    private void UpdateDrag() => UpdateDragAt(PointToClient(Cursor.Position).Y);

    // clientY = la position verticale de la souris dans la surface (séparé pour pouvoir être testé sans souris réelle)
    private void UpdateDragAt(int clientY)
    {
        var contentY = clientY + Scroll;
        if (_dragGroup != null)
        {
            var drag = _dragGroup;
            drag.DisplayY = contentY - _grab;
            var center = drag.DisplayY + GroupHeight(drag) / 2;
            var firstScene = _groups.Count(g => g.Audio);
            var others = _groups.Where(g => g != drag && !g.Audio).ToList();
            var newIndex = firstScene + others.Count(g => g.TargetY + GroupHeight(g) / 2 < center);
            var current = _groups.IndexOf(drag);
            if (newIndex != current) { _groups.RemoveAt(current); _groups.Insert(newIndex, drag); }
        }
        else if (_dragRow != null && _dragRowGroup != null)
        {
            var group = _dragRowGroup; var drag = _dragRow;
            var relY = contentY - (group.DisplayY + Inset + HeaderH(group)) - _grab;
            drag.DisplayY = Math.Clamp(relY, 0, Math.Max(0, (group.Rows.Count - 1) * RowH));
            var center = drag.DisplayY + RowH / 2f;
            var newIndex = group.Rows.Count(r => r != drag && r.TargetY + RowH / 2f < center);
            var current = group.Rows.IndexOf(drag);
            if (newIndex != current) { group.Rows.RemoveAt(current); group.Rows.Insert(newIndex, drag); }
        }
    }

    private void EndDrag()
    {
        Capture = false;
        if (_dragGroup != null)
        {
            _dragGroup.Lifted = false;
            SceneOrderChanged?.Invoke(_groups.Where(g => !g.Audio).Select(g => g.Key).ToList());
        }
        else if (_dragRow != null && _dragRowGroup != null)
        {
            _dragRow.Lifted = false;
            RowOrderChanged?.Invoke(_dragRowGroup.Key, _dragRowGroup.Rows.Select(r => r.Key).ToList());
        }
        _dragGroup = null; _dragRow = null; _dragRowGroup = null;
        StartTimer();   // l'élément lâché glisse jusqu'à sa place
    }
}
