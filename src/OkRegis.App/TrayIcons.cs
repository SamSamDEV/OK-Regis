using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using OkRegis.Core.Orchestration;

namespace OkRegis.App;

/// <summary>Icône de la zone de notification : un « R » avec un point de couleur selon l'état.</summary>
public static class TrayIcons
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    private static readonly Dictionary<RegisState, Icon> Cache = new();

    public static Icon For(RegisState state)
    {
        if (Cache.TryGetValue(state, out var icon)) return icon;
        var dot = state switch
        {
            RegisState.Listening => Color.FromArgb(52, 199, 130),   // vert : à l'écoute
            RegisState.Connecting => Color.FromArgb(245, 166, 35),  // orange : connexion
            RegisState.Paused => Color.FromArgb(239, 83, 80),       // rouge : en pause
            _ => Color.FromArgb(139, 147, 161),                     // gris : en veille
        };
        return Cache[state] = Make(dot);
    }

    private static Icon Make(Color dot)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var bg = new SolidBrush(Color.FromArgb(79, 70, 229))) g.FillEllipse(bg, 1, 1, 30, 30);
            using (var font = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var white = new SolidBrush(Color.White))
            {
                var size = g.MeasureString("R", font);
                g.DrawString("R", font, white, (32 - size.Width) / 2 + 0.5f, (32 - size.Height) / 2);
            }
            using (var ring = new SolidBrush(Color.White)) g.FillEllipse(ring, 18, 18, 14, 14);
            using (var d = new SolidBrush(dot)) g.FillEllipse(d, 20, 20, 10, 10);
        }
        var handle = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        return icon;
    }
}
