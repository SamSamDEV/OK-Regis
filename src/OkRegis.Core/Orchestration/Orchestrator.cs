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
    private readonly object _stateLock = new();
    private volatile bool _obsRunning;

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
        // _obsRunning change tout de suite (avant d'attendre son tour) : une boucle de reconnexion en cours le voit et s'arrête.
        _watcher.ObsStarted += () => { _obsRunning = true; _ = RunSafe(OnObsStartedAsync); };
        _watcher.ObsStopped += () => { _obsRunning = false; _ = RunSafe(OnObsStoppedAsync); };
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
        lock (_stateLock)
        {
            State = RegisState.Paused; // d'abord l'état : une connexion en cours ne rouvrira pas le micro
            _mic.Stop();
        }
        Changed?.Invoke();
    }

    public void Resume() => _ = RunSafe(async () =>
    {
        bool connected, micFailed = false;
        lock (_stateLock)
        {
            if (State != RegisState.Paused) return;
            if (!_obsRunning) { State = RegisState.Idle; connected = false; }
            else if (_obs.IsConnected) { micFailed = !StartListeningLocked(); connected = true; }
            else { State = RegisState.Connecting; connected = false; }
        }
        if (micFailed) AddHistory("", MicFailedText, false);
        Changed?.Invoke();
        if (!connected && State == RegisState.Connecting) await ConnectLoopAsync();
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
        if (State == RegisState.Paused) return;
        await ConnectLoopAsync();
    }

    private Task OnObsStoppedAsync()
    {
        _mic.Stop();
        _ = _obs.DisconnectAsync();
        SetStateUnlessPaused(RegisState.Idle);
        return Task.CompletedTask;
    }

    private async Task OnObsDisconnectedAsync()
    {
        _mic.Stop();
        if (!SetStateUnlessPaused(RegisState.Idle)) return;
        if (_obsRunning) await ConnectLoopAsync(); // OBS tourne encore : on se reconnecte
    }

    // Réessaie de se connecter tant qu'OBS tourne (le serveur WebSocket peut être lent à démarrer).
    private async Task ConnectLoopAsync()
    {
        if (!SetStateUnlessPaused(RegisState.Connecting)) return;
        while (_obsRunning && State == RegisState.Connecting)
        {
            try
            {
                var password = _settings.ObsPasswordEncrypted.Length == 0 ? "" : _secrets.Unprotect(_settings.ObsPasswordEncrypted);
                await _obs.ConnectAsync(_settings.ObsHost, _settings.ObsPort, password);
                Snapshot = await _obs.GetSnapshotAsync();
                RebuildGrammar();
                bool micFailed;
                lock (_stateLock)
                {
                    if (State != RegisState.Connecting) return; // pause (ou OBS fermé) pendant la connexion : micro fermé
                    micFailed = !StartListeningLocked();
                }
                if (micFailed) AddHistory("", MicFailedText, false);
                Changed?.Invoke();
                return;
            }
            catch (Exception)
            {
                await Task.Delay(RetryDelay);
            }
        }
        if (_obsRunning == false) SetStateUnlessPaused(RegisState.Idle);
    }

    // Ouvre le micro et passe en "À l'écoute" (à appeler avec _stateLock pris).
    // Renvoie false si le micro (ou le modèle vocal) n'a pas pu s'ouvrir : on reste en veille, sans faire semblant d'écouter.
    private bool StartListeningLocked()
    {
        _mic.Start(_settings.MicDeviceName);
        State = _mic.IsRunning ? RegisState.Listening : RegisState.Idle;
        return _mic.IsRunning;
    }

    private const string MicFailedText = "Micro ou modèle vocal indisponible : Régis n'écoute pas";

    // Change l'état sauf si l'utilisateur a mis Régis en pause. Renvoie false si la pause a gagné.
    private bool SetStateUnlessPaused(RegisState state)
    {
        lock (_stateLock)
        {
            if (State == RegisState.Paused) return false;
            State = state;
        }
        Changed?.Invoke();
        return true;
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
