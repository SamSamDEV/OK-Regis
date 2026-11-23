using OBSWebsocketDotNet;
using OkRegis.Core.Commands;
using OkRegis.Core.Obs;
using OkRegis.Core.Localization;

namespace OkRegis.App.Obs;

/// <summary>
/// Parle à OBS par son serveur WebSocket (v5). Ne tourne que quand OBS est ouvert.
/// La reconnexion automatique est faite par l'Orchestrator (il réessaie tant qu'OBS tourne).
/// </summary>
public class ObsWebSocketClient : IObsClient, IDisposable
{
    private readonly OBSWebsocket _obs = new();
    private System.Threading.Timer? _debounce;
    private bool _wasConnected;

    public bool IsConnected => _obs.IsConnected;
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action? SnapshotChanged;

    public ObsWebSocketClient()
    {
        // Tout ce qui change dans OBS → on prévient (une seule fois par rafale, voir NotifyChanged).
        _obs.CurrentProgramSceneChanged += (_, _) => NotifyChanged();
        _obs.SceneListChanged += (_, _) => NotifyChanged();
        _obs.SceneCreated += (_, _) => NotifyChanged();
        _obs.SceneRemoved += (_, _) => NotifyChanged();
        _obs.SceneNameChanged += (_, _) => NotifyChanged();
        _obs.SceneItemCreated += (_, _) => NotifyChanged();
        _obs.SceneItemRemoved += (_, _) => NotifyChanged();
        _obs.SceneItemListReindexed += (_, _) => NotifyChanged();
        _obs.SceneItemEnableStateChanged += (_, _) => NotifyChanged();
        _obs.InputCreated += (_, _) => NotifyChanged();
        _obs.InputRemoved += (_, _) => NotifyChanged();
        _obs.InputNameChanged += (_, _) => NotifyChanged();
        _obs.Disconnected += (_, _) =>
        {
            if (!_wasConnected) return; // échec de connexion : l'appelant le sait déjà (exception)
            _wasConnected = false;
            Disconnected?.Invoke();
        };
    }

    public async Task ConnectAsync(string host, int port, string password)
    {
        if (_obs.IsConnected) _obs.Disconnect();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler onConnected = (_, _) => done.TrySetResult();
        EventHandler<OBSWebsocketDotNet.Communication.ObsDisconnectionInfo> onFailed =
            (_, info) => done.TrySetException(new InvalidOperationException(
                string.IsNullOrWhiteSpace(info.DisconnectReason)
                    ? Loc.T("Connexion à OBS impossible (code {0}). Vérifie qu'OBS est ouvert, que le serveur WebSocket est activé, et le port / mot de passe.", info.ObsCloseCode)
                    : Loc.T("Connexion à OBS refusée : ") + info.DisconnectReason));
        _obs.Connected += onConnected;
        _obs.Disconnected += onFailed;
        try
        {
            _obs.ConnectAsync($"ws://{host}:{port}", password);
            if (await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(8))) != done.Task)
                throw new TimeoutException(Loc.T("OBS ne répond pas"));
            await done.Task;
            _wasConnected = true;
            Connected?.Invoke();
        }
        finally
        {
            _obs.Connected -= onConnected;
            _obs.Disconnected -= onFailed;
        }
    }

    public Task DisconnectAsync()
    {
        _wasConnected = false;
        if (_obs.IsConnected) _obs.Disconnect();
        return Task.CompletedTask;
    }

    // ---- lecture ----

    public Task<ObsSnapshot> GetSnapshotAsync() => Task.Run(() =>
    {
        var list = _obs.GetSceneList();
        var scenes = list.Scenes.Select(s => s.Name).Reverse().ToList(); // OBS les renvoie en ordre inversé
        var current = list.CurrentProgramSceneName;

        // Les sources de CHAQUE scène, comme dans OBS (groupes dépliés).
        var groups = Groups();
        var byScene = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var scene in scenes)
        {
            try { byScene[scene] = ListSources(scene, false, groups, new HashSet<string>()).Distinct().ToList(); }
            catch (Exception) { byScene[scene] = new List<string>(); }
        }
        var sources = current is not null && byScene.TryGetValue(current, out var currentSources) ? currentSources : new List<string>();

        // Une source a de l'audio si OBS accepte de donner son état muet.
        var audio = new List<string>();
        var muted = new Dictionary<string, bool>();
        foreach (var input in _obs.GetInputList())
        {
            try { muted[input.InputName] = _obs.GetInputMute(input.InputName); audio.Add(input.InputName); }
            catch (Exception) { /* pas de piste audio */ }
        }
        return new ObsSnapshot(scenes, current, sources, audio, byScene, muted);
    });

    // Les éléments d'une scène ou d'un groupe : (nom de la source, numéro de l'élément).
    private List<(string Name, int Id)> Items(string container, bool isGroup) => isGroup
        ? _obs.GetGroupSceneItemList(container).Select(o => ((string)o["sourceName"]!, (int)o["sceneItemId"]!)).ToList()
        : _obs.GetSceneItemList(container).Select(i => (i.SourceName, i.ItemId)).ToList();

    private HashSet<string> Groups()
    {
        try { return new HashSet<string>(_obs.GetGroupList()); }
        catch (Exception) { return new HashSet<string>(); }
    }

    // Les sources d'une scène, en entrant dans les groupes.
    private List<string> ListSources(string container, bool isGroup, HashSet<string> groups, HashSet<string> visited)
    {
        var names = new List<string>();
        if (!visited.Add(container)) return names; // protège d'une boucle
        foreach (var (name, _) in Items(container, isGroup))
        {
            names.Add(name);
            if (groups.Contains(name)) names.AddRange(ListSources(name, true, groups, visited));
        }
        return names;
    }

    // ---- actions ----

    public Task SetCurrentSceneAsync(string scene) => Task.Run(() => _obs.SetCurrentProgramScene(scene));

    public Task SetSourceVisibleAsync(string scene, string source, bool visible) => Task.Run(() =>
    {
        if (!TrySetEnabled(scene, false, source, visible, Groups(), new HashSet<string>()))
            throw new InvalidOperationException(Loc.T("Source introuvable : {0}", source));
    });

    // Cherche la source dans la scène, puis dans ses groupes, et change sa visibilité.
    private bool TrySetEnabled(string container, bool isGroup, string source, bool visible, HashSet<string> groups, HashSet<string> visited)
    {
        if (!visited.Add(container)) return false;
        var items = Items(container, isGroup);
        foreach (var (name, id) in items)
            if (name == source) { _obs.SetSceneItemEnabled(container, id, visible); return true; }
        foreach (var (name, _) in items.Where(i => groups.Contains(i.Name)))
            if (TrySetEnabled(name, true, source, visible, groups, visited)) return true;
        return false;
    }

    public Task SetMutedAsync(string audioSource, bool muted) => Task.Run(() => _obs.SetInputMute(audioSource, muted));

    public Task<bool> ToggleMuteAsync(string audioSource) => Task.Run(() =>
    {
        var now = !_obs.GetInputMute(audioSource);
        _obs.SetInputMute(audioSource, now);
        return now;
    });

    public Task AdjustVolumeDbAsync(string audioSource, double deltaDb) => Task.Run(() =>
    {
        var current = _obs.GetInputVolume(audioSource).VolumeDb;
        var target = Math.Clamp(current + deltaDb, -100.0, 26.0); // limites d'OBS
        _obs.SetInputVolume(audioSource, (float)target, true);
    });

    public Task RecordAsync(RecordAction action) => Task.Run(() =>
    {
        switch (action)
        {
            case RecordAction.Start: _obs.StartRecord(); break;
            case RecordAction.Stop: _obs.StopRecord(); break;
            default: _obs.ToggleRecordPause(); break; // pause / reprise
        }
    });

    public Task StreamAsync(bool start) => Task.Run(() => { if (start) _obs.StartStream(); else _obs.StopStream(); });

    // Plusieurs changements d'affilée → une seule alerte, 300 ms après le dernier.
    private void NotifyChanged()
    {
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(_ => SnapshotChanged?.Invoke(), null, 300, Timeout.Infinite);
    }

    public void Dispose()
    {
        _debounce?.Dispose();
        if (_obs.IsConnected) _obs.Disconnect();
    }
}
