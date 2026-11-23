namespace OkRegis.App;

/// <summary>Une ligne (source) avec un fin trait de séparation en haut.</summary>
public class RowPanel : Panel
{
    private bool _dragging;

    /// <summary>Vrai pendant qu'on déplace la ligne (fond un peu différent).</summary>
    public bool Dragging { get => _dragging; set { _dragging = value; BackColor = value ? Theme.SurfaceAlt : Theme.Surface; } }

    public RowPanel() { DoubleBuffered = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, 12, 0, Width - 12, 0);
    }
}

/// <summary>Une carte de scène : un en-tête et, dessous, la liste de ses sources. La hauteur s'adapte au contenu.</summary>
public class GroupCard : Card
{
    public FlowLayoutPanel Stack { get; } = new()
    {
        FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink, Location = new Point(4, 4), Tag = "card", Margin = Padding.Empty,
    };

    public GroupCard() { Controls.Add(Stack); }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        FitChildren();
    }

    /// <summary>Ajuste la largeur des éléments à la carte, puis la hauteur de la carte à son contenu.</summary>
    public void FitChildren()
    {
        var width = Math.Max(200, Width - 8);
        Stack.Width = width;
        foreach (Control c in Stack.Controls)
        {
            c.Margin = Padding.Empty; c.Width = width;
            if (c.Tag is "rows") foreach (Control row in c.Controls) row.Width = width;
        }
        Stack.PerformLayout();
        var height = Stack.Height + 8;
        if (Height != height) Height = height;
    }
}

/// <summary>Glisser-déposer pour réordonner des éléments d'un même conteneur, en tirant une poignée.</summary>
public static class DragReorder
{
    /// <param name="handle">la poignée que l'on attrape</param>
    /// <param name="item">l'élément déplacé (son Tag = son nom ; seuls les frères avec un Tag texte comptent)</param>
    /// <param name="dropped">appelé au relâchement avec le nouvel ordre (les Tags dans l'ordre)</param>
    /// <param name="dragging">appelé avec true au début et false à la fin (retour visuel)</param>
    public static void Attach(Control handle, Control item, Action<IReadOnlyList<string>> dropped, Action<bool>? dragging = null)
    {
        var active = false;
        handle.Cursor = Cursors.SizeAll;
        handle.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            active = true; handle.Capture = true; dragging?.Invoke(true);
        };
        handle.MouseMove += (_, _) =>
        {
            if (!active || item.Parent is not { } parent) return;
            var y = parent.PointToClient(Cursor.Position).Y;
            var siblings = parent.Controls.Cast<Control>().Where(c => c != item && c.Tag is string).ToList();
            if (siblings.Count == 0) return;
            var target = siblings.FirstOrDefault(c => y < c.Top + c.Height / 2);
            var current = parent.Controls.GetChildIndex(item);
            int newIndex;
            if (target != null)
            {
                var targetIndex = parent.Controls.GetChildIndex(target);
                newIndex = current < targetIndex ? targetIndex - 1 : targetIndex;
            }
            else newIndex = Math.Max(current, parent.Controls.GetChildIndex(siblings[^1]));
            if (newIndex != current) parent.Controls.SetChildIndex(item, newIndex);
            AutoScroll(parent);
        };
        handle.MouseUp += (_, _) =>
        {
            if (!active) return;
            active = false; handle.Capture = false; dragging?.Invoke(false);
            if (item.Parent is { } parent)
                dropped(parent.Controls.Cast<Control>().Where(c => c.Tag is string).Select(c => (string)c.Tag!).ToList());
        };
    }

    // Quand on tire près du bord haut/bas de la liste, elle défile.
    private static void AutoScroll(Control from)
    {
        for (var c = from; c != null; c = c.Parent)
        {
            if (c is CardList scroller)
            {
                var y = scroller.PointToClient(Cursor.Position).Y;
                var offset = -scroller.AutoScrollPosition.Y;
                if (y < 40) scroller.AutoScrollPosition = new Point(0, Math.Max(0, offset - 24));
                else if (y > scroller.ClientSize.Height - 40) scroller.AutoScrollPosition = new Point(0, offset + 24);
                return;
            }
        }
    }
}
