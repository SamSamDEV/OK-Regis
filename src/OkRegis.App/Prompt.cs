namespace OkRegis.App;

/// <summary>Petite boîte pour demander un texte. validate renvoie un message d'erreur (la boîte reste ouverte) ou null.</summary>
public static class Prompt
{
    public static string? Ask(string title, string label, string initial = "", Func<string, string?>? validate = null, IWin32Window? owner = null)
    {
        using var form = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, ClientSize = new Size(440, 170),
        };
        var lbl = new Label { Text = label, Left = 16, Top = 14, Width = 408, Height = 44 };
        var box = new TextBox { Text = initial, Left = 16, Top = 62, Width = 408 };
        var error = new Label { Tag = "danger", Left = 16, Top = 92, Width = 408, Height = 36 };
        var ok = new Button { Text = "Ajouter", Tag = "primary", Left = 244, Top = 128, Width = 90 };
        var cancel = new Button { Text = "Annuler", Left = 340, Top = 128, Width = 84, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { lbl, box, error, ok, cancel });
        form.CancelButton = cancel; form.AcceptButton = ok;
        ok.Click += (_, _) =>
        {
            var text = box.Text.Trim();
            var problem = text.Length == 0 ? "Écris un mot." : validate?.Invoke(text);
            if (problem != null) { error.Text = problem; box.Focus(); box.SelectAll(); return; }
            form.DialogResult = DialogResult.OK;
        };
        Theme.Apply(form);
        return form.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
    }
}
