using OkRegis.App.Obs;
using OkRegis.App.Speech;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.App;

/// <summary>
/// Fenêtre de réglages. Au premier lancement elle sert d'assistant de configuration
/// (même contenu, avec une introduction qui explique comment activer le WebSocket d'OBS).
/// </summary>
public class SettingsForm : Form
{
    private static readonly string[] CommandLabels =
    {
        "Changer de scène", "Afficher une source", "Masquer une source", "Couper un son", "Rétablir un son",
        "Monter le volume", "Baisser le volume", "Lancer l'enregistrement", "Arrêter l'enregistrement",
        "Mettre l'enregistrement en pause", "Lancer le stream (déconseillé)", "Arrêter le stream",
    };
    private static readonly string[] ThemeLabels = { "Automatique (comme Windows)", "Clair", "Sombre (nuit)" };
    private static readonly string[] ModeLabels = { "Voix de Régis (ou mes sons)", "Juste un bip", "Silence" };

    private readonly AppSettings _s;
    private readonly ISecretProtector _secrets;
    private readonly Vocabulary _vocab;

    private readonly TextBox _name = new();
    private readonly Label _nameWarning = new() { Tag = "danger", AutoSize = true };
    private readonly ComboBox _mic = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _output = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly Label _testResult = new() { AutoSize = true };
    private readonly NumericUpDown _volume = new() { Minimum = 1, Maximum = 20, DecimalPlaces = 1 };
    private readonly NumericUpDown _confidence = new() { Minimum = 0.1m, Maximum = 0.95m, DecimalPlaces = 2, Increment = 0.05m };
    private readonly CheckBox _autostart = new() { Text = "Lancer OK Régis au démarrage de Windows", AutoSize = true };
    private readonly CheckedListBox _commands = new() { CheckOnClick = true, Dock = DockStyle.Fill };

    public SettingsForm(AppSettings settings, ISecretProtector secrets, Vocabulary vocab, bool firstRun)
    {
        _s = settings; _secrets = secrets; _vocab = vocab;
        Text = firstRun ? "OK Régis — première configuration" : "OK Régis — réglages";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 680);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

        var tabs = new NavTabs();
        tabs.Add("Général", BuildGeneralTab(firstRun));
        tabs.Add("Commandes", WrapTab("Coche les commandes que Régis a le droit d'exécuter.", _commands));

        var ok = new Button { Text = "Enregistrer", Tag = "primary", DialogResult = DialogResult.OK, Width = 110 };
        var cancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Width = 80 };
        ok.Click += (_, _) => Apply();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        Controls.Add(tabs); Controls.Add(buttons);
        AcceptButton = ok; CancelButton = cancel;

        Load();
        Theme.Apply(this);
    }

    private Control BuildGeneralTab(bool firstRun)
    {
        var page = new Panel();
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8), AutoScroll = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        if (firstRun)
        {
            var intro = new Label
            {
                AutoSize = true, MaximumSize = new Size(500, 0), Padding = new Padding(0, 0, 0, 8),
                Text = "Bienvenue ! Choisis le nom de ton assistant, ton micro, puis active le serveur WebSocket dans OBS :\n" +
                       "Outils → Paramètres du serveur WebSocket → « Activer le serveur WebSocket » (note le port et le mot de passe).",
            };
            table.Controls.Add(intro); table.SetColumnSpan(intro, 2);
        }

        AddRow(table, "Nom de l'assistant", _name);
        table.Controls.Add(new Label()); table.Controls.Add(_nameWarning);
        AddRow(table, "Apparence", _theme);
        AddRow(table, "Micro", _mic);
        AddRow(table, "Réponses de Régis", _mode);
        AddRow(table, "Sortie audio des réponses", _output);
        AddRow(table, "Adresse d'OBS", _host);
        AddRow(table, "Port WebSocket", _port);
        AddRow(table, "Mot de passe WebSocket", _password);
        var test = new Button { Text = "Tester la connexion à OBS", AutoSize = true };
        test.Click += async (_, _) => await TestConnectionAsync();
        var testRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        _testResult.Margin = new Padding(8, 8, 0, 0);
        testRow.Controls.Add(test); testRow.Controls.Add(_testResult);
        table.Controls.Add(new Label()); table.Controls.Add(testRow);
        AddRow(table, "Pas de volume (dB)", _volume);
        AddRow(table, "Seuil de confiance", _confidence);
        table.Controls.Add(_autostart); table.SetColumnSpan(_autostart, 2);

        _name.TextChanged += (_, _) => CheckName();
        page.Controls.Add(table);
        return page;
    }

    private static Control WrapTab(string help, Control content)
    {
        var page = new Panel();
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = help, AutoSize = true });
        panel.Controls.Add(content);
        page.Controls.Add(panel);
        return page;
    }

    private static void AddRow(TableLayoutPanel t, string label, Control c)
    {
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
        c.Dock = DockStyle.Fill;
        t.Controls.Add(c);
    }

    // ---- réglages → écran ----
    private new void Load()
    {
        _name.Text = _s.AssistantName;
        _mic.Items.Add("(micro par défaut)"); _mic.Items.AddRange(VoskMicListener.ListDevices().ToArray());
        _mic.SelectedIndex = Math.Max(0, _mic.FindString(_s.MicDeviceName ?? ""));
        _output.Items.Add("(sortie par défaut)"); _output.Items.AddRange(WindowsResponder.ListDevices().ToArray());
        _output.SelectedIndex = Math.Max(0, _output.FindString(_s.OutputDeviceName ?? ""));
        _mode.Items.AddRange(ModeLabels); _mode.SelectedIndex = _s.VoiceMode switch { ResponseMode.Beep => 1, ResponseMode.Silent => 2, _ => 0 };
        _theme.Items.AddRange(ThemeLabels); _theme.SelectedIndex = (int)_s.Theme;
        _host.Text = _s.ObsHost; _port.Value = _s.ObsPort;
        _volume.Value = (decimal)_s.VolumeStepDb; _confidence.Value = (decimal)_s.MinConfidence;
        _autostart.Checked = _s.FirstRunDone ? Autostart.IsEnabled() : _s.StartWithWindows; // au premier lancement : la valeur par défaut (cochée)
        foreach (var kind in Enum.GetValues<CommandKind>())
            _commands.Items.Add(CommandLabels[(int)kind], _s.IsEnabled(kind));
        CheckName();
    }

    // Prévient si Vosk ne connaît pas le nom choisi.
    private void CheckName()
    {
        var text = _name.Text.Trim();
        _nameWarning.MaximumSize = new Size(330, 0);
        _nameWarning.Text = text.Length == 0 ? ""
            : !_vocab.CanSpeak(text, out _) ? "⚠ Régis ne pourra pas entendre ce nom : essaie une autre orthographe."
            : NameCheck.AssistantNameProblem(text, _s.Nicknames.Values) is { } problem ? "⚠ " + problem
            : "";
    }

    private async Task TestConnectionAsync()
    {
        _testResult.ForeColor = Theme.Muted; _testResult.Text = "Connexion…";
        using var client = new ObsWebSocketClient();
        try
        {
            await client.ConnectAsync(_host.Text.Trim(), (int)_port.Value, PasswordToUse());
            var snap = await client.GetSnapshotAsync();
            _testResult.ForeColor = Theme.Success; _testResult.Text = $"✔ Connecté ({snap.Scenes.Count} scènes)";
        }
        catch (Exception ex)
        {
            _testResult.ForeColor = Theme.Danger; _testResult.Text = "✘ " + ex.Message;
        }
    }

    // Mot de passe tapé, sinon celui déjà enregistré.
    private string PasswordToUse() => _password.Text.Length > 0 ? _password.Text
        : _s.ObsPasswordEncrypted.Length > 0 ? _secrets.Unprotect(_s.ObsPasswordEncrypted) : "";

    // ---- écran → réglages ----
    private void Apply()
    {
        _s.AssistantName = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : _s.AssistantName;
        _s.MicDeviceName = _mic.SelectedIndex > 0 ? (string)_mic.SelectedItem! : null;
        _s.OutputDeviceName = _output.SelectedIndex > 0 ? (string)_output.SelectedItem! : null;
        _s.VoiceMode = _mode.SelectedIndex switch { 1 => ResponseMode.Beep, 2 => ResponseMode.Silent, _ => ResponseMode.WindowsVoice };
        _s.Theme = (ThemeMode)_theme.SelectedIndex;
        _s.ObsHost = _host.Text.Trim().Length > 0 ? _host.Text.Trim() : "localhost";
        _s.ObsPort = (int)_port.Value;
        if (_password.Text.Length > 0) _s.ObsPasswordEncrypted = _secrets.Protect(_password.Text);
        _s.VolumeStepDb = (double)_volume.Value; _s.MinConfidence = (double)_confidence.Value;
        _s.StartWithWindows = _autostart.Checked;
        for (var i = 0; i < _commands.Items.Count; i++) _s.EnabledCommands[(CommandKind)i] = _commands.GetItemChecked(i);

        _s.FirstRunDone = true;
    }
}
