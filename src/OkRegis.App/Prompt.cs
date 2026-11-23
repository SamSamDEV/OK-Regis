namespace OkRegis.App;

/// <summary>Petite boîte pour demander un texte à l'utilisateur.</summary>
public static class Prompt
{
    public static string? Ask(string title, string label, string initial = "")
    {
        using var form = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(380, 110),
        };
        var lbl = new Label { Text = label, Left = 12, Top = 12, Width = 356, Height = 36 };
        var box = new TextBox { Text = initial, Left = 12, Top = 52, Width = 356 };
        var ok = new Button { Text = "OK", Left = 212, Top = 78, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", Left = 293, Top = 78, Width = 75, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
        form.AcceptButton = ok; form.CancelButton = cancel;
        return form.ShowDialog() == DialogResult.OK && box.Text.Trim().Length > 0 ? box.Text.Trim() : null;
    }
}
