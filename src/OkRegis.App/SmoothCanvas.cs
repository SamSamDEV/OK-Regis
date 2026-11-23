using System.Diagnostics;

namespace OkRegis.App;

/// <summary>
/// Base d'une surface qui défile et se dessine elle-même (sans contrôle Windows par ligne).
/// Elle fournit le défilement adouci à la molette et une minuterie d'animation ≈ 64 images/s
/// qui ne tourne QUE pendant qu'une animation est en cours : au repos, rien ne s'exécute.
/// </summary>
public abstract class SmoothCanvas : ScrollableControl
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastTickMs;
    private float _scrollPos;
    private int _contentHeight;

    /// <summary>Où le défilement doit arriver (la molette le déplace, l'animation le rejoint).</summary>
    protected float ScrollTarget;

    /// <summary>Levé quand l'utilisateur fait défiler (molette ou barre).</summary>
    public event Action? Scrolled;

    protected SmoothCanvas()
    {
        AutoScroll = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>La hauteur totale du contenu (règle la barre de défilement).</summary>
    protected int ContentHeight
    {
        get => _contentHeight;
        set { _contentHeight = value; if (AutoScrollMinSize.Height != value) AutoScrollMinSize = new Size(0, value); }
    }

    /// <summary>Le défilement actuel, en pixels depuis le haut.</summary>
    protected int Scroll => -AutoScrollPosition.Y;
    protected float MaxScroll => Math.Max(0, ContentHeight - ClientSize.Height);

    protected void SyncScroll() { _scrollPos = ScrollTarget = Scroll; }

    protected void StartTimer()
    {
        if (_timer.Enabled) return;
        _lastTickMs = _clock.ElapsedMilliseconds;
        _timer.Start();
    }

    /// <summary>Fait avancer les animations de la sous-classe. Renvoie vrai s'il en reste.</summary>
    protected virtual bool StepContent(float dt) => false;

    /// <summary>Vrai tant que la minuterie doit tourner même sans animation (ex. un glisser en cours).</summary>
    protected virtual bool KeepRunning => false;

    protected static float Ease(float current, float target, float rate, float dt, ref bool animating)
    {
        var diff = target - current;
        if (Math.Abs(diff) < 0.4f) return target;
        animating = true;
        return current + diff * (1 - MathF.Exp(-rate * dt));
    }

    private void Tick()
    {
        var now = _clock.ElapsedMilliseconds;
        var dt = Math.Min(0.05f, (now - _lastTickMs) / 1000f);
        _lastTickMs = now;

        var animating = StepContent(dt);
        _scrollPos = Ease(_scrollPos, Math.Clamp(ScrollTarget, 0, MaxScroll), 18, dt, ref animating);
        var rounded = (int)Math.Round(_scrollPos);
        if (rounded != Scroll) AutoScrollPosition = new Point(0, rounded);

        Invalidate();
        if (!animating && !KeepRunning) _timer.Stop();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // défilement adouci : la molette déplace une cible, l'animation la rejoint
        ScrollTarget = Math.Clamp(ScrollTarget - e.Delta * 0.8f, 0, MaxScroll);
        Scrolled?.Invoke();
        StartTimer();
        if (e is HandledMouseEventArgs h) h.Handled = true;
    }

    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        SyncScroll();   // barre de défilement tirée à la main : la cible suit
        Scrolled?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
