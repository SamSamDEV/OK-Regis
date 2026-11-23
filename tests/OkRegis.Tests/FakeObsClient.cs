using OkRegis.Core.Commands;
using OkRegis.Core.Obs;

namespace OkRegis.Tests;

/// <summary>Faux OBS : note les appels reçus, peut simuler une panne.</summary>
public class FakeObsClient : IObsClient
{
    public List<string> Calls { get; } = new();
    public Exception? FailWith { get; set; }
    public ObsSnapshot Snapshot { get; set; } =
        new(new[] { "LoL" }, "LoL", new[] { "AFK" }, new[] { "Spotify" });
    public bool IsConnected { get; private set; }
    public int ConnectFailures { get; set; }
    /// <summary>Si renseigné, ConnectAsync attend que cette tâche se termine (simule une connexion lente).</summary>
    public Task? ConnectGate { get; set; }

    public event Action? Connected;
    public event Action? Disconnected;
    public event Action? SnapshotChanged;

    public void RaiseSnapshotChanged() => SnapshotChanged?.Invoke();
    public void RaiseDisconnected() { IsConnected = false; Disconnected?.Invoke(); }

    private Task Record(string call) { if (FailWith != null) throw FailWith; Calls.Add(call); return Task.CompletedTask; }

    public async Task ConnectAsync(string host, int port, string password)
    {
        if (ConnectGate != null) await ConnectGate;
        if (ConnectFailures > 0) { ConnectFailures--; throw new InvalidOperationException("OBS pas prêt"); }
        Calls.Add($"connect {host}:{port}"); IsConnected = true; Connected?.Invoke();
    }
    public Task DisconnectAsync() { IsConnected = false; return Task.CompletedTask; }
    public Task<ObsSnapshot> GetSnapshotAsync() => Task.FromResult(Snapshot);
    public Task SetCurrentSceneAsync(string s) => Record($"scene {s}");
    public Task SetSourceVisibleAsync(string scene, string source, bool v) => Record($"visible {scene}/{source}={v}");
    public Task SetMutedAsync(string a, bool m) => Record($"mute {a}={m}");
    public Task AdjustVolumeDbAsync(string a, double d) => Record(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"volume {a}{d:+0.0;-0.0}"));
    public Task RecordAsync(RecordAction a) => Record($"record {a}");
    public Task StreamAsync(bool start) => Record($"stream {start}");
}
