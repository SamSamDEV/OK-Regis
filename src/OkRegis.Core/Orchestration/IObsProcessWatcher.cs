namespace OkRegis.Core.Orchestration;

/// <summary>Surveille si OBS est lancé. Ne lève un événement que quand l'état CHANGE.</summary>
public interface IObsProcessWatcher
{
    event Action ObsStarted;
    event Action ObsStopped;
    void Start();
}
