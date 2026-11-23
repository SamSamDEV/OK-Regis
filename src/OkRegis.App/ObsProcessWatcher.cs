using System.Diagnostics;
using OkRegis.Core.Orchestration;

namespace OkRegis.App;

/// <summary>Regarde toutes les 3 secondes si obs64.exe tourne. N'alerte que quand ça change.</summary>
public class ObsProcessWatcher : IObsProcessWatcher, IDisposable
{
    private System.Threading.Timer? _timer;
    private bool _running;

    public event Action? ObsStarted;
    public event Action? ObsStopped;

    public void Start() => _timer = new System.Threading.Timer(_ => Check(), null, 0, 3000);

    private void Check()
    {
        var processes = Process.GetProcessesByName("obs64");
        var now = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        if (now == _running) return;
        _running = now;
        if (now) ObsStarted?.Invoke(); else ObsStopped?.Invoke();
    }

    public void Dispose() => _timer?.Dispose();
}
