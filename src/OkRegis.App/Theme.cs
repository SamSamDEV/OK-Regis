using System.Runtime.InteropServices;
using Microsoft.Win32;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>Couleurs et style de l'appli, en thème clair ou sombre (« nuit »).</summary>
public static class Theme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subIdList);

    public static bool Dark { get; private set; }
    public static Color Bg, Surface, Border, Text, Muted, Accent, AccentText, Danger, Success, Warning;
    public static readonly Font BaseFont = new("Segoe UI", 9.5f);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 15f);

    public static void Set(ThemeMode mode)
    {
        Dark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemUsesDark());
        if (Dark)
        {
            Bg = Color.FromArgb(20, 22, 27); Surface = Color.FromArgb(30, 33, 40); Border = Color.FromArgb(48, 53, 63);
            Text = Color.FromArgb(230, 232, 236); Muted = Color.FromArgb(139, 147, 161);
            Accent = Color.FromArgb(99, 102, 241); AccentText = Color.White;
            Danger = Color.FromArgb(239, 83, 80); Success = Color.FromArgb(52, 199, 130); Warning = Color.FromArgb(245, 166, 35);
        }
        else
        {
            Bg = Color.FromArgb(243, 244, 247); Surface = Color.White; Border = Color.FromArgb(217, 220, 227);
            Text = Color.FromArgb(27, 31, 39); Muted = Color.FromArgb(107, 114, 128);
            Accent = Color.FromArgb(79, 70, 229); AccentText = Color.White;
            Danger = Color.FromArgb(220, 38, 38); Success = Color.FromArgb(22, 163, 74); Warning = Color.FromArgb(217, 119, 6);
        }
    }

    private static bool SystemUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Applique les couleurs à un contrôle et à tous ses enfants.</summary>
    public static void Apply(Control c)
    {
        switch (c)
        {
            case Form f:
                f.BackColor = Bg; f.ForeColor = Text; f.Font = BaseFont;
                if (f.IsHandleCreated) DarkTitleBar(f.Handle); else f.HandleCreated += (_, _) => DarkTitleBar(f.Handle);
                break;
            case Button b:
                b.FlatStyle = FlatStyle.Flat; b.Cursor = Cursors.Hand; b.UseVisualStyleBackColor = false;
                b.FlatAppearance.BorderSize = 1;
                if ((b.Tag as string) == "primary")
                {
                    b.BackColor = Accent; b.ForeColor = AccentText; b.FlatAppearance.BorderColor = Accent;
                }
                else
                {
                    b.BackColor = Surface; b.ForeColor = Text; b.FlatAppearance.BorderColor = Border;
                }
                b.Padding = new Padding(10, 3, 10, 3); b.MinimumSize = new Size(0, 32);
                break;
            case ListView lv:
                lv.BackColor = Surface; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None;
                if (lv.IsHandleCreated) SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                else lv.HandleCreated += (_, _) => SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                break;
            case TextBox t: t.BackColor = Surface; t.ForeColor = Text; t.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox cb: cb.FlatStyle = FlatStyle.Flat; cb.BackColor = Surface; cb.ForeColor = Text; break;
            case NumericUpDown n: n.BackColor = Surface; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle; break;
            case CheckedListBox cl: cl.BackColor = Surface; cl.ForeColor = Text; cl.BorderStyle = BorderStyle.None; break;
            case Label l:
                l.BackColor = Color.Transparent;
                l.ForeColor = (l.Tag as string) switch { "muted" => Muted, "danger" => Danger, _ => Text };
                break;
            case RadioButton { Appearance: Appearance.Button }: break; // boutons d'onglets : stylés par NavTabs
            case CheckBox or RadioButton: c.ForeColor = Text; c.BackColor = Color.Transparent; break;
            case NavTabs nav: nav.BackColor = Bg; nav.ApplyTheme(); break;
            case Panel or TableLayoutPanel or FlowLayoutPanel or UserControl:
                c.BackColor = (c.Tag as string) == "card" ? Surface : Bg;
                break;
        }
        foreach (Control child in c.Controls) Apply(child);
    }

    private static void DarkTitleBar(IntPtr handle)
    {
        var on = Dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)); // barre de titre sombre (Windows 10/11)
    }
}
