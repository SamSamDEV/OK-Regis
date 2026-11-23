using System.Diagnostics;

namespace OkRegis.App;

/// <summary>Mesure le processeur et la mémoire de Régis lui-même.</summary>
public class ResourceMeter
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private DateTime _lastTime = DateTime.UtcNow;

    public double CpuPercent { get; private set; }
    public long WorkingSetMb { get; private set; }

    /// <summary>Met à jour les valeurs (à appeler au plus une fois par seconde).</summary>
    public void Sample()
    {
        _process.Refresh();
        var now = DateTime.UtcNow;
        var cpu = _process.TotalProcessorTime;
        var elapsed = (now - _lastTime).TotalMilliseconds;
        if (elapsed > 0)
            CpuPercent = (cpu - _lastCpu).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100.0;
        _lastCpu = cpu; _lastTime = now;
        WorkingSetMb = _process.WorkingSet64 / (1024 * 1024);
    }

    public string Text => $"CPU {CpuPercent:0.0} % · {WorkingSetMb} {OkRegis.Core.Localization.Loc.T("Mo")}";
}
