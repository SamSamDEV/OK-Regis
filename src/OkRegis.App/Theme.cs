using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>Couleurs et style de l'appli, en thème clair (« bright ») ou sombre (« nuit »). Une seule couleur d'accent : le bleu.</summary>
public static class Theme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subIdList);

    public static bool Dark { get; private set; }
    public static Color Bg, Surface, SurfaceAlt, Border, Text, Muted, Accent, AccentText, ChipBack, Danger, Success, Warning;
    public static readonly Font BaseFont = new("Segoe UI", 9.5f);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 15f);
    public static readonly Font CardTitleFont = new("Segoe UI Semibold", 11.5f);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f);
    public static readonly Font SymbolFont12 = new("Segoe UI Symbol", 12f);
    public static readonly Font SymbolFont14 = new("Segoe UI Symbol", 14f);

    public static void Set(ThemeMode mode)
    {
        Dark = mode == ThemeMode.Dark || (mode == ThemeMode.System && SystemUsesDark());
        if (Dark)
        {
            Bg = Color.FromArgb(16, 18, 22); Surface = Color.FromArgb(26, 29, 35); SurfaceAlt = Color.FromArgb(38, 42, 50);
            Border = Color.FromArgb(44, 49, 58); Text = Color.FromArgb(232, 234, 238); Muted = Color.FromArgb(141, 149, 163);
            Accent = Color.FromArgb(59, 130, 246); AccentText = Color.White; ChipBack = Color.FromArgb(30, 58, 110);
            Danger = Color.FromArgb(239, 68, 68); Success = Color.FromArgb(34, 197, 94); Warning = Color.FromArgb(245, 158, 11);
        }
        else
        {
            Bg = Color.FromArgb(245, 246, 248); Surface = Color.White; SurfaceAlt = Color.FromArgb(240, 242, 245);
            Border = Color.FromArgb(227, 229, 234); Text = Color.FromArgb(20, 23, 31); Muted = Color.FromArgb(107, 114, 128);
            Accent = Color.FromArgb(37, 99, 235); AccentText = Color.White; ChipBack = Color.FromArgb(219, 234, 254);
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
                b.FlatAppearance.BorderSize = 0;
                var primary = (b.Tag as string) == "primary";
                b.BackColor = primary ? Accent : SurfaceAlt;
                b.ForeColor = primary ? AccentText : Text;
                b.FlatAppearance.MouseOverBackColor = primary ? ControlPaint.Light(Accent, 0.15f) : Border;
                b.MinimumSize = new Size(0, 34);
                b.SizeChanged -= Round; b.SizeChanged += Round; Round(b, EventArgs.Empty);
                break;
            case ListView lv:
                lv.BackColor = Surface; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None;
                if (lv.IsHandleCreated) SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                else lv.HandleCreated += (_, _) => SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                break;
            case TextBox t: t.BackColor = SurfaceAlt; t.ForeColor = Text; t.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox cb:
                cb.FlatStyle = FlatStyle.Flat; cb.BackColor = SurfaceAlt; cb.ForeColor = Text;
                if (cb.IsHandleCreated) SetWindowTheme(cb.Handle, Dark ? "DarkMode_CFD" : "Explorer", null); // liste déroulante sombre
                else cb.HandleCreated += (_, _) => SetWindowTheme(cb.Handle, Dark ? "DarkMode_CFD" : "Explorer", null);
                break;
            case NumericUpDown n: n.BackColor = SurfaceAlt; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle; break;
            case CheckedListBox cl: cl.BackColor = Surface; cl.ForeColor = Text; cl.BorderStyle = BorderStyle.None; break;
            case Label l:
                l.BackColor = BackFor(l); // fond opaque : évite de redessiner le parent à chaque fois (plus fluide)
                l.ForeColor = (l.Tag as string) switch { "muted" => Muted, "danger" => Danger, "accent" => Accent, _ => Text };
                break;
            case RadioButton { Appearance: Appearance.Button }: break; // boutons d'onglets : stylés par NavTabs
            case CheckBox or RadioButton: c.ForeColor = Text; c.BackColor = BackFor(c); break;
            case NavTabs nav: nav.BackColor = Bg; nav.ApplyTheme(); break;
            case Card card: card.BackColor = (card.Tag as string) == "flat" ? Surface : Bg; card.Invalidate(); break;
            case Chip chip: chip.BackColor = Surface; chip.Invalidate(); break;
            case NameRow row: row.BackColor = row.Dragging ? SurfaceAlt : Surface; row.Invalidate(); break;
            case CardList list:
                list.BackColor = Bg;
                if (list.IsHandleCreated) SetWindowTheme(list.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null); // barre de défilement sombre
                else list.HandleCreated += (_, _) => SetWindowTheme(list.Handle, Dark ? "DarkMode_Explorer" : "Explorer", null);
                break;
            case Panel or TableLayoutPanel or FlowLayoutPanel or UserControl:
                c.BackColor = (c.Tag as string) is "card" or "rows" ? Surface : Bg;
                break;
        }
        foreach (Control child in c.Controls) Apply(child);
    }

    // Couleur de fond d'un contrôle posé sur un parent : une carte est peinte en Surface, sinon on prend la couleur du parent.
    private static Color BackFor(Control c) => c.Parent is Card ? Surface : c.Parent?.BackColor ?? Bg;

    // Coins arrondis pour les boutons.
    private static void Round(object? sender, EventArgs e)
    {
        if (sender is not Button b || b.Width < 10 || b.Height < 10) return;
        b.Region?.Dispose();
        b.Region = new Region(RoundedRect(new Rectangle(0, 0, b.Width, b.Height), 8));
    }

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DarkTitleBar(IntPtr handle)
    {
        var on = Dark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)); // barre de titre sombre (Windows 10/11)
    }
}
