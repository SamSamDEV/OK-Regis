using OkRegis.App.Obs;
using OkRegis.App.Speech;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;
using OkRegis.Core.Localization;

namespace OkRegis.App;

/// <summary>
/// Fenêtre de réglages. Au premier lancement elle sert d'assistant de configuration
/// (même contenu, avec une introduction qui explique comment activer le WebSocket d'OBS).
/// </summary>
public class SettingsForm : Form
{
    private static readonly string[] CommandLabels =
    {
        Loc.T("Changer de scène"), Loc.T("Afficher une source"), Loc.T("Masquer une source"), Loc.T("Couper un son"), Loc.T("Rétablir un son"),
        Loc.T("Monter le volume"), Loc.T("Baisser le volume"), Loc.T("Lancer l'enregistrement"), Loc.T("Arrêter l'enregistrement"),
        Loc.T("Mettre l'enregistrement en pause"), Loc.T("Lancer le stream (déconseillé)"), Loc.T("Arrêter le stream"),
    };
    private static readonly string[] ThemeLabels = { Loc.T("Automatique (comme Windows)"), Loc.T("Clair"), Loc.T("Sombre (nuit)") };
    private static readonly string[] ModeLabels = { Loc.T("Voix de Régis (ou mes sons)"), Loc.T("Juste un bip"), Loc.T("Silence") };

    private readonly AppSettings _s;
    private readonly ISecretProtector _secrets;
    private readonly Vocabulary _vocab;

    private readonly TextBox _name = new();
    private readonly Label _nameWarning = new() { Tag = "danger", AutoSize = true };
    private readonly ComboBox _mic = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _output = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _host = new();
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535 };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly Label _testResult = new() { AutoSize = true };
    private readonly NumericUpDown _gain = new() { Minimum = 0, Maximum = 30, DecimalPlaces = 0 };
    private readonly MicMeter _meter = new() { Dock = DockStyle.Fill };
    private volatile float _gainLive;   // lu par le fil audio du témoin de niveau
    private readonly NumericUpDown _volume = new() { Minimum = 1, Maximum = 20, DecimalPlaces = 1 };
    private readonly NumericUpDown _confidence = new() { Minimum = 0.1m, Maximum = 0.95m, DecimalPlaces = 2, Increment = 0.05m };
    private readonly CheckBox _saveCpu = new() { Text = Loc.T("Économiser le processeur (n'analyse le micro que quand je parle)"), AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = Loc.T("Lancer OK Régis au démarrage de Windows"), AutoSize = true };
    private readonly CheckedListBox _commands = new() { CheckOnClick = true, Dock = DockStyle.Fill };

    public SettingsForm(AppSettings settings, ISecretProtector secrets, Vocabulary vocab, bool firstRun)
    {
        _s = settings; _secrets = secrets; _vocab = vocab;
        Text = firstRun ? Loc.T("OK Régis — première configuration") : Loc.T("OK Régis — réglages");
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 750);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;

        var tabs = new NavTabs();
        tabs.Add(Loc.T("Général"), BuildGeneralTab(firstRun));
        tabs.Add(Loc.T("Commandes"), WrapTab(Loc.T("Coche les commandes que Régis a le droit d'exécuter."), _commands));

        var ok = new Button { Text = Loc.T("Enregistrer"), Tag = "primary", DialogResult = DialogResult.OK, Width = 110 };
        var cancel = new Button { Text = Loc.T("Annuler"), DialogResult = DialogResult.Cancel, Width = 80 };
        ok.Click += (_, _) => Apply();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
        buttons.Controls.AddRange(new Control[] { cancel, ok });
        Controls.Add(tabs); Controls.Add(buttons);
        AcceptButton = ok; CancelButton = cancel;

        Load();
        Theme.Apply(this);

        // Témoin de niveau : le micro n'est ouvert que pendant que cette fenêtre est affichée.
        _gain.ValueChanged += (_, _) => _gainLive = (float)_gain.Value;
        _mic.SelectedIndexChanged += (_, _) => StartMeter();
        Shown += (_, _) => StartMeter();
        FormClosed += (_, _) => _meter.Stop();
    }

    private void StartMeter() => _meter.Start(_mic.SelectedIndex > 0 ? (string)_mic.SelectedItem! : null, () => _gainLive);

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
                Text = Loc.T("Bienvenue ! Choisis le nom de ton assistant, ton micro, puis active le serveur WebSocket dans OBS :\n") +
                       Loc.T("Outils → Paramètres du serveur WebSocket → « Activer le serveur WebSocket » (note le port et le mot de passe)."),
            };
            table.Controls.Add(intro); table.SetColumnSpan(intro, 2);
        }

        AddRow(table, Loc.T("Nom de l'assistant"), _name);
        table.Controls.Add(new Label()); table.Controls.Add(_nameWarning);
        if (!firstRun)
        {
            AddRow(table, Loc.T("Langue"), _language);
            table.Controls.Add(new Label());
            table.Controls.Add(new Label { Text = Loc.T("Le changement de langue s'applique au redémarrage de l'appli."), Tag = "muted", AutoSize = true, Font = Theme.SmallFont });
        }
        AddRow(table, Loc.T("Apparence"), _theme);
        AddRow(table, Loc.T("Micro"), _mic);
        AddRow(table, Loc.T("Boost du micro (dB)"), _gain);
        table.Controls.Add(_meter); table.SetColumnSpan(_meter, 2);
        AddRow(table, Loc.T("Réponses de Régis"), _mode);
        AddRow(table, Loc.T("Sortie audio des réponses"), _output);
        AddRow(table, Loc.T("Adresse d'OBS"), _host);
        AddRow(table, Loc.T("Port WebSocket"), _port);
        AddRow(table, Loc.T("Mot de passe WebSocket"), _password);
        var test = new Button { Text = Loc.T("Tester la connexion à OBS"), AutoSize = true };
        test.Click += async (_, _) => await TestConnectionAsync();
        var testRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        _testResult.Margin = new Padding(8, 8, 0, 0);
        testRow.Controls.Add(test); testRow.Controls.Add(_testResult);
        table.Controls.Add(new Label()); table.Controls.Add(testRow);
        AddRow(table, Loc.T("Pas de volume (dB)"), _volume);
        AddRow(table, Loc.T("Seuil de confiance"), _confidence);
        table.Controls.Add(_saveCpu); table.SetColumnSpan(_saveCpu, 2);
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
        _mic.Items.Add(Loc.T("(micro par défaut)")); _mic.Items.AddRange(VoskMicListener.ListDevices().ToArray());
        _mic.SelectedIndex = Math.Max(0, _mic.FindString(_s.MicDeviceName ?? ""));
        _output.Items.Add(Loc.T("(sortie par défaut)")); _output.Items.AddRange(WindowsResponder.ListDevices().ToArray());
        _output.SelectedIndex = Math.Max(0, _output.FindString(_s.OutputDeviceName ?? ""));
        _mode.Items.AddRange(ModeLabels); _mode.SelectedIndex = _s.VoiceMode switch { ResponseMode.Beep => 1, ResponseMode.Silent => 2, _ => 0 };
        _theme.Items.AddRange(ThemeLabels); _theme.SelectedIndex = (int)_s.Theme;
        _language.Items.AddRange(new object[] { Loc.NameOf(Language.French), Loc.NameOf(Language.English), Loc.NameOf(Language.Spanish) });
        _language.SelectedIndex = (int)_s.Language;
        _host.Text = _s.ObsHost; _port.Value = _s.ObsPort;
        _saveCpu.Checked = _s.SaveCpuWhenSilent;
        _gain.Value = (decimal)Math.Clamp(_s.MicGainDb, 0, 30); _gainLive = (float)_gain.Value;
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
            : !_vocab.CanSpeak(text, out _) ? Loc.T("⚠ Régis ne pourra pas entendre ce nom : essaie une autre orthographe.")
            : NameCheck.AssistantNameProblem(text, _s.Nicknames.Values, OkRegis.Core.Languages.LanguagePack.For(_s.Language)) is { } problem ? "⚠ " + problem
            : "";
    }

    private async Task TestConnectionAsync()
    {
        _testResult.ForeColor = Theme.Muted; _testResult.Text = Loc.T("Connexion…");
        using var client = new ObsWebSocketClient();
        try
        {
            await client.ConnectAsync(_host.Text.Trim(), (int)_port.Value, PasswordToUse());
            var snap = await client.GetSnapshotAsync();
            _testResult.ForeColor = Theme.Success; _testResult.Text = Loc.T("✔ Connecté ({0} scènes)", snap.Scenes.Count);
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
        _s.Language = (Language)_language.SelectedIndex;
        _s.ObsHost = _host.Text.Trim().Length > 0 ? _host.Text.Trim() : "localhost";
        _s.ObsPort = (int)_port.Value;
        if (_password.Text.Length > 0) _s.ObsPasswordEncrypted = _secrets.Protect(_password.Text);
        _s.SaveCpuWhenSilent = _saveCpu.Checked;
        _s.MicGainDb = (double)_gain.Value;
        _s.VolumeStepDb = (double)_volume.Value; _s.MinConfidence = (double)_confidence.Value;
        _s.StartWithWindows = _autostart.Checked;
        for (var i = 0; i < _commands.Items.Count; i++) _s.EnabledCommands[(CommandKind)i] = _commands.GetItemChecked(i);

        _s.FirstRunDone = true;
    }
}
