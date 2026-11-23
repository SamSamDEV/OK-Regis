using OkRegis.Core.Commands;
using OkRegis.Core.Obs;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;
using OkRegis.Core.Text;

namespace OkRegis.Core.Orchestration;

/// <summary>
/// Le chef d'orchestre : OBS s'ouvre → on se connecte et on écoute ; une phrase arrive → on la comprend,
/// on l'exécute dans OBS et on répond ; OBS se ferme → on ferme le micro et on retourne en veille.
/// </summary>
public class Orchestrator
{
    private const int MaxHistory = 200;

    private readonly AppSettings _settings;
    private readonly IObsClient _obs;
    private readonly IMicListener _mic;
    private readonly IResponder _voice;
    private readonly IObsProcessWatcher _watcher;
    private readonly Vocabulary _vocab;
    private readonly ISecretProtector _secrets;
    private readonly List<HistoryEntry> _history = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _obsRunning;

    public RegisState State { get; private set; } = RegisState.Idle;
    public ObsSnapshot? Snapshot { get; private set; }
    public GrammarResult? Grammar { get; private set; }
    public string LastHeard { get; private set; } = "";
    /// <summary>Attente entre deux essais de connexion à OBS.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public event Action? Changed;

    public IReadOnlyList<HistoryEntry> History { get { lock (_history) return _history.ToList(); } }

    public Orchestrator(AppSettings settings, IObsClient obs, IMicListener mic, IResponder voice,
        IObsProcessWatcher watcher, Vocabulary vocab, ISecretProtector secrets)
    {
        _settings = settings; _obs = obs; _mic = mic; _voice = voice;
        _watcher = watcher; _vocab = vocab; _secrets = secrets;
    }

    public void Start()
    {
        _watcher.ObsStarted += () => _ = RunSafe(OnObsStartedAsync);
        _watcher.ObsStopped += () => _ = RunSafe(OnObsStoppedAsync);
        _obs.Disconnected += () => _ = RunSafe(OnObsDisconnectedAsync);
        _obs.SnapshotChanged += () => _ = RunSafe(RefreshSnapshotAsync);
        _mic.PhraseHeard += heard => _ = RunSafe(() => OnPhraseHeardAsync(heard));
        _voice.SpeakingStarted += () => _mic.Muted = true;
        _voice.SpeakingEnded += () => _mic.Muted = false;
        SetState(RegisState.Idle);
        _watcher.Start();
    }

    public void Pause()
    {
        _mic.Stop();
        SetState(RegisState.Paused);
    }

    public void Resume() => _ = RunSafe(async () =>
    {
        if (State != RegisState.Paused) return;
        if (!_obsRunning) { SetState(RegisState.Idle); return; }
        if (_obs.IsConnected) { _mic.Start(_settings.MicDeviceName); SetState(RegisState.Listening); }
        else await ConnectLoopAsync();
    });

    /// <summary>Reconstruit la liste de mots (ex. après un changement de nom ou de surnom).</summary>
    public void RebuildGrammar()
    {
        if (Snapshot is null) return;
        Grammar = GrammarBuilder.Build(_settings, Snapshot, _vocab);
        _mic.SetGrammar(GrammarBuilder.ToVoskJson(Grammar));
        Changed?.Invoke();
    }

    // ---- événements ----

    private async Task OnObsStartedAsync()
    {
        _obsRunning = true;
        if (State == RegisState.Paused) return;
        await ConnectLoopAsync();
    }

    private Task OnObsStoppedAsync()
    {
        _obsRunning = false;
        _mic.Stop();
        _ = _obs.DisconnectAsync();
        if (State != RegisState.Paused) SetState(RegisState.Idle);
        return Task.CompletedTask;
    }

    private async Task OnObsDisconnectedAsync()
    {
        _mic.Stop();
        if (State == RegisState.Paused) return;
        SetState(RegisState.Idle);
        if (_obsRunning) await ConnectLoopAsync(); // OBS tourne encore : on se reconnecte
    }

    // Réessaie de se connecter tant qu'OBS tourne (le serveur WebSocket peut être lent à démarrer).
    private async Task ConnectLoopAsync()
    {
        SetState(RegisState.Connecting);
        while (_obsRunning && State == RegisState.Connecting)
        {
            try
            {
                var password = _settings.ObsPasswordEncrypted.Length == 0 ? "" : _secrets.Unprotect(_settings.ObsPasswordEncrypted);
                await _obs.ConnectAsync(_settings.ObsHost, _settings.ObsPort, password);
                Snapshot = await _obs.GetSnapshotAsync();
                RebuildGrammar();
                _mic.Start(_settings.MicDeviceName);
                SetState(RegisState.Listening);
                return;
            }
            catch (Exception)
            {
                await Task.Delay(RetryDelay);
            }
        }
        if (State == RegisState.Connecting) SetState(RegisState.Idle);
    }

    private async Task RefreshSnapshotAsync()
    {
        if (!_obs.IsConnected) return;
        Snapshot = await _obs.GetSnapshotAsync();
        RebuildGrammar();
    }

    private async Task OnPhraseHeardAsync(Heard heard)
    {
        if (string.IsNullOrWhiteSpace(heard.Text)) return;
        LastHeard = heard.Text;

        if (heard.Confidence < _settings.MinConfidence || Snapshot is null)
        {
            AddHistory(heard.Text, "Pas assez sûr, ignoré", false);
            return;
        }
        var parsed = new CommandParser(_settings).Parse(heard.Text, Snapshot);
        if (parsed.Status != ParseStatus.Ok)
        {
            AddHistory(heard.Text, parsed.Status switch
            {
                ParseStatus.NoWakeWord => "Pas pour moi",
                ParseStatus.UnknownVerb => "Commande inconnue",
                ParseStatus.UnknownTarget => "Scène ou source inconnue",
                _ => "Commande désactivée",
            }, false);
            return;
        }

        var result = await new CommandExecutor(_obs, _settings).ExecuteAsync(parsed.Command!, Snapshot);
        AddHistory(heard.Text, result.Success ? $"{parsed.Command!.Kind} {result.Name}".Trim() : "Échec dans OBS", result.Success);
        var sentence = new ResponsePicker(_settings).Pick(result.ActionKey, result.Name);
        await _voice.RespondAsync(result.ActionKey, sentence);
    }

    // ---- outils ----

    // Un événement ne doit jamais faire planter l'appli.
    private async Task RunSafe(Func<Task> action)
    {
        await _gate.WaitAsync();
        try { await action(); }
        catch (Exception ex) { AddHistory("", "Erreur interne : " + ex.Message, false); }
        finally { _gate.Release(); }
    }

    private void SetState(RegisState state) { State = state; Changed?.Invoke(); }

    private void AddHistory(string heard, string result, bool success)
    {
        lock (_history)
        {
            _history.Add(new HistoryEntry(DateTime.Now, heard, result, success));
            if (_history.Count > MaxHistory) _history.RemoveRange(0, _history.Count - MaxHistory);
        }
        Changed?.Invoke();
    }
}
