using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class OrchestratorTests
{
    private readonly FakeObsClient _obs = new();
    private readonly FakeMic _mic = new();
    private readonly FakeResponder _voice = new();
    private readonly FakeWatcher _watcher = new();
    private readonly AppSettings _settings = new();
    private readonly Orchestrator _o;

    public OrchestratorTests()
    {
        _voice.Mic = _mic;
        var vocab = new Vocabulary(new[] { "régis", "bob", "mets", "lol", "ok", "hey", "afk" });
        _o = new Orchestrator(_settings, _obs, _mic, _voice, _watcher, vocab, new PlainProtector()) { RetryDelay = TimeSpan.Zero };
        _o.Start();
    }

    [Fact]
    public void Au_depart_on_est_en_veille_micro_ferme()
    {
        Assert.Equal(RegisState.Idle, _o.State);
        Assert.False(_mic.Running);
    }

    [Fact]
    public void Obs_demarre_on_ecoute_une_seule_fois_avec_la_grammaire()
    {
        _watcher.RaiseStarted();
        Assert.Equal(RegisState.Listening, _o.State);
        Assert.True(_mic.Running);
        Assert.Equal(1, _mic.StartCount);
        Assert.Single(_mic.Grammars);
    }

    [Fact]
    public void Obs_ferme_micro_ferme_et_veille()
    {
        _watcher.RaiseStarted();
        _watcher.RaiseStopped();
        Assert.Equal(RegisState.Idle, _o.State);
        Assert.False(_mic.Running);
    }

    [Fact]
    public void Changement_dans_obs_reconstruit_la_grammaire()
    {
        _watcher.RaiseStarted();
        _obs.RaiseSnapshotChanged();
        Assert.Equal(2, _mic.Grammars.Count);
    }

    [Fact]
    public void Phrase_comprise_execute_repond_et_journalise()
    {
        _watcher.RaiseStarted();
        _mic.Say("régis mets lol");
        Assert.Contains("scene LoL", _obs.Calls);
        Assert.Single(_voice.Said);
        Assert.True(_o.History.Last().Success);
        Assert.Equal("régis mets lol", _o.LastHeard);
    }

    [Fact]
    public void Confiance_trop_basse_ne_fait_rien()
    {
        _watcher.RaiseStarted();
        _mic.Say("régis mets lol", 0.3);
        Assert.DoesNotContain("scene LoL", _obs.Calls);
        Assert.False(_o.History.Last().Success);
        Assert.Empty(_voice.Said);
    }

    [Fact]
    public void Le_micro_est_coupe_pendant_que_regis_parle_puis_rouvert()
    {
        _watcher.RaiseStarted();
        _mic.Say("régis mets lol");
        Assert.True(_voice.MicMutedWhileSpeaking);
        Assert.False(_mic.Muted);
    }

    [Fact]
    public void Pause_ferme_le_micro_et_obs_qui_demarre_ne_le_rouvre_pas()
    {
        _watcher.RaiseStarted();
        _o.Pause();
        Assert.False(_mic.Running);
        _watcher.RaiseStopped();
        _watcher.RaiseStarted();
        Assert.Equal(RegisState.Paused, _o.State);
        Assert.False(_mic.Running);
        _o.Resume();
        Assert.True(_mic.Running);
        Assert.Equal(RegisState.Listening, _o.State);
    }

    [Fact]
    public void Panne_obs_pendant_une_commande_ne_leve_pas_dexception()
    {
        _watcher.RaiseStarted();
        _obs.FailWith = new InvalidOperationException("OBS fermé");
        _mic.Say("régis mets lol");
        Assert.False(_o.History.Last().Success);
    }

    [Fact]
    public void Connexion_perdue_micro_ferme_puis_reconnexion()
    {
        _watcher.RaiseStarted();
        _obs.RaiseDisconnected();
        Assert.Equal(RegisState.Listening, _o.State); // OBS tourne encore → reconnecté
        Assert.Equal(2, _mic.StartCount);
    }

    [Fact]
    public void Connexion_qui_echoue_est_reessayee()
    {
        _obs.ConnectFailures = 2;
        _watcher.RaiseStarted();
        Assert.Equal(RegisState.Listening, _o.State);
    }

    [Fact]
    public void Historique_limite_a_200()
    {
        _watcher.RaiseStarted();
        for (var i = 0; i < 250; i++) _mic.Say("régis mets lol");
        Assert.Equal(200, _o.History.Count);
    }

    [Fact]
    public void Nouveau_nom_de_lassistant_reconstruit_la_grammaire()
    {
        _watcher.RaiseStarted();
        _settings.AssistantName = "bob";
        _o.RebuildGrammar();
        Assert.Contains("bob", _mic.Grammars.Last());
    }
}
