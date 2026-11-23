namespace OkRegis.App;

/// <summary>Des onglets modernes (boutons plats) qui suivent le thème clair/sombre.</summary>
public class NavTabs : UserControl
{
    private readonly FlowLayoutPanel _bar = new() { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8, 6, 8, 0), WrapContents = false };
    private readonly Panel _body = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly List<(RadioButton Button, Control Page)> _tabs = new();

    public NavTabs()
    {
        Dock = DockStyle.Fill;
        Controls.Add(_body); Controls.Add(_bar); // la barre au-dessus du corps
    }

    public void Add(string title, Control page)
    {
        var first = _tabs.Count == 0;
        var button = new RadioButton
        {
            Text = title, Appearance = Appearance.Button, AutoSize = true, FlatStyle = FlatStyle.Flat,
            Padding = new Padding(12, 4, 12, 4), Margin = new Padding(0, 0, 6, 0), Cursor = Cursors.Hand, Checked = first,
        };
        button.FlatAppearance.BorderSize = 0;
        page.Dock = DockStyle.Fill; page.Visible = first;
        button.CheckedChanged += (_, _) => { page.Visible = button.Checked; Restyle(); };
        _bar.Controls.Add(button); _body.Controls.Add(page);
        _tabs.Add((button, page));
        Restyle();
    }

    public void ApplyTheme() => Restyle();

    // Onglet actif = couleur d'accent ; les autres restent discrets.
    private void Restyle()
    {
        _bar.BackColor = Theme.Bg; _body.BackColor = Theme.Bg;
        foreach (var (button, _) in _tabs)
        {
            button.BackColor = button.Checked ? Theme.Accent : Theme.Surface;
            button.ForeColor = button.Checked ? Theme.AccentText : Theme.Muted;
            button.FlatAppearance.CheckedBackColor = Theme.Accent;
        }
    }
}
