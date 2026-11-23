namespace OkRegis.App;

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

/// <summary>Glisser-déposer pour réordonner des éléments d'un même conteneur.</summary>
public static class DragReorder
{
    /// <summary>
    /// Un déplacement en cours. Move() à chaque mouvement de souris : l'élément se place en direct entre ses frères.
    /// End() au relâchement : renvoie le nouvel ordre. Seuls les frères dont le Tag est un texte comptent.
    /// </summary>
    public sealed class Session
    {
        private readonly Control _item;
        private readonly Action<IReadOnlyList<string>> _dropped;

        public Session(Control item, Action<IReadOnlyList<string>> dropped) { _item = item; _dropped = dropped; }

        public void Move()
        {
            if (_item.Parent is not { } parent) return;
            var y = parent.PointToClient(Cursor.Position).Y;
            var siblings = parent.Controls.Cast<Control>().Where(c => c != _item && c.Tag is string).ToList();
            if (siblings.Count == 0) return;
            var target = siblings.FirstOrDefault(c => y < c.Top + c.Height / 2);
            var current = parent.Controls.GetChildIndex(_item);
            int newIndex;
            if (target != null)
            {
                var targetIndex = parent.Controls.GetChildIndex(target);
                newIndex = current < targetIndex ? targetIndex - 1 : targetIndex;
            }
            else newIndex = Math.Max(current, parent.Controls.GetChildIndex(siblings[^1]));
            if (newIndex != current) parent.Controls.SetChildIndex(_item, newIndex);
            AutoScroll(parent);
        }

        public void End()
        {
            if (_item.Parent is { } parent)
                _dropped(parent.Controls.Cast<Control>().Where(c => c.Tag is string).Select(c => (string)c.Tag!).ToList());
        }
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
