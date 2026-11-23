using NAudio.Wave;
using OkRegis.App.Speech;
using OkRegis.Core.Localization;
using OkRegis.Core.Speech;

namespace OkRegis.App;

/// <summary>
/// Indicateur de niveau du micro, en direct (fenêtre de réglages) : une barre + un message qui dit
/// si le niveau est trop faible, correct ou trop fort pour la reconnaissance vocale.
/// Il ouvre le micro seulement pendant que la fenêtre de réglages est affichée.
/// </summary>
public class MicMeter : Control
{
    private const double MinDb = -60;
    private WaveIn? _waveIn;
    private Func<double> _gainDb = () => 0;
    private double _levelDb = -120;
    private double _peakDb = -120;          // le plus fort niveau récent (la voix), qui retombe après quelques secondes
    private DateTime _peakAt = DateTime.MinValue;
    private bool _failed;

    public MicMeter()
    {
        DoubleBuffered = true; Height = 56;
        SetStyle(ControlStyles.Selectable, false);
    }

    /// <summary>Ouvre le micro choisi (null = par défaut) ; le boost est relu en continu.</summary>
    public void Start(string? deviceName, Func<double> gainDb)
    {
        Stop();
        _gainDb = gainDb; _levelDb = _peakDb = -120; _failed = false;
        try
        {
            _waveIn = new WaveIn { DeviceNumber = VoskMicListener.FindDevice(deviceName), WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 100 };
            _waveIn.DataAvailable += OnData;
            _waveIn.StartRecording();
        }
        catch (Exception) { _failed = true; _waveIn?.Dispose(); _waveIn = null; }
        Invalidate();
    }

    public void Stop()
    {
        if (_waveIn == null) return;
        _waveIn.DataAvailable -= OnData;
        try { _waveIn.StopRecording(); } catch (Exception) { }
        _waveIn.Dispose(); _waveIn = null;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        // Même calcul que l'écoute : volume, plus le boost choisi.
        var level = Math.Min(0, AudioGain.LevelDb(AudioGain.Rms(e.Buffer, e.BytesRecorded)) + _gainDb());
        var now = DateTime.Now;
        _levelDb = level;
        if (level > _peakDb || (now - _peakAt).TotalSeconds > 4) { _peakDb = level; _peakAt = now; }
        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Invalidate));
    }

    // Le message selon le niveau de la voix.
    private (string Text, Color Color) Status()
    {
        if (_failed) return (Loc.T("Micro indisponible pour la mesure."), Theme.Danger);
        if (_peakDb < -55) return (Loc.T("Parle normalement pour tester le niveau."), Theme.Muted);
        return AudioGain.Classify(_peakDb) switch
        {
            MicLevel.TooLow => (Loc.T("Trop faible : augmente le boost ci-dessus."), Theme.Danger),
            MicLevel.Low => (Loc.T("Un peu faible : un peu plus de boost aiderait."), Theme.Warning),
            MicLevel.Good => (Loc.T("Niveau parfait pour la reconnaissance."), Theme.Success),
            _ => (Loc.T("Trop fort : le son sature, baisse le boost."), Theme.Danger),
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;

        TextRenderer.DrawText(g, Loc.T("Niveau du micro"), Theme.BaseFont, new Rectangle(0, 0, Width, 18), Theme.Text, flags);
        var bar = new Rectangle(0, 22, Width - 1, 12);
        using (var path = Theme.RoundedRect(bar, 6)) using (var back = new SolidBrush(Theme.SurfaceAlt)) g.FillPath(back, path);

        // La zone « parfaite » (-30 à -6 dB) en fond léger, puis le niveau actuel.
        int X(double db) => bar.X + (int)(Math.Clamp((db - MinDb) / -MinDb, 0, 1) * bar.Width);
        var zone = new Rectangle(X(-30), bar.Y, Math.Max(1, X(-6) - X(-30)), bar.Height);
        using (var zoneBrush = new SolidBrush(Color.FromArgb(50, Theme.Success))) g.FillRectangle(zoneBrush, zone);
        var fill = Math.Max(0, X(_levelDb) - bar.X);
        if (fill > 4)
        {
            var color = AudioGain.Classify(_levelDb) switch { MicLevel.Good => Theme.Success, MicLevel.Loud => Theme.Danger, _ => Theme.Warning };
            using var path = Theme.RoundedRect(new Rectangle(bar.X, bar.Y, fill, bar.Height), 6);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        var (text, textColor) = Status();
        TextRenderer.DrawText(g, text, Theme.SmallFont, new Rectangle(0, 38, Width, 16), textColor, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Stop();
        base.Dispose(disposing);
    }
}
