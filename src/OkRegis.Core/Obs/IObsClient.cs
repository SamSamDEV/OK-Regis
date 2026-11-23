using OkRegis.Core.Commands;

namespace OkRegis.Core.Obs;

public enum RecordAction { Start, Stop, Pause }

/// <summary>Tout ce que Régis demande à OBS. La vraie version (WebSocket) est dans l'appli.</summary>
public interface IObsClient
{
    bool IsConnected { get; }
    event Action Connected;
    event Action Disconnected;
    /// <summary>OBS a changé (scènes, sources...). Levé une seule fois par rafale.</summary>
    event Action SnapshotChanged;

    Task ConnectAsync(string host, int port, string password);
    Task DisconnectAsync();
    Task<ObsSnapshot> GetSnapshotAsync();
    Task SetCurrentSceneAsync(string scene);
    Task SetSourceVisibleAsync(string scene, string source, bool visible);
    Task SetMutedAsync(string audioSource, bool muted);
    Task AdjustVolumeDbAsync(string audioSource, double deltaDb);
    Task RecordAsync(RecordAction action);
    Task StreamAsync(bool start);
}
