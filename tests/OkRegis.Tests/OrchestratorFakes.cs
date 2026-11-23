using OkRegis.Core.Orchestration;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;

namespace OkRegis.Tests;

public class FakeMic : IMicListener
{
    public event Action<Heard>? PhraseHeard;
    public bool Running { get; private set; }
    public int StartCount { get; private set; }
    public List<string> Grammars { get; } = new();
    public bool Muted { get; set; }
    public void Start(string? d) { Running = true; StartCount++; }
    public void Stop() => Running = false;
    public void SetGrammar(string g) => Grammars.Add(g);
    public void Say(string text, double conf = 0.9) => PhraseHeard?.Invoke(new Heard(text, conf));
    public void Dispose() { }
}

public class FakeResponder : IResponder
{
    public event Action? SpeakingStarted;
    public event Action? SpeakingEnded;
    public FakeMic? Mic { get; set; }
    public List<string> Said { get; } = new();
    public bool MicMutedWhileSpeaking { get; private set; }
    public Task RespondAsync(string key, string text)
    {
        SpeakingStarted?.Invoke();
        MicMutedWhileSpeaking = Mic?.Muted ?? false;
        Said.Add(text);
        SpeakingEnded?.Invoke();
        return Task.CompletedTask;
    }
}

public class FakeWatcher : IObsProcessWatcher
{
    public event Action? ObsStarted;
    public event Action? ObsStopped;
    public void Start() { }
    public void RaiseStarted() => ObsStarted?.Invoke();
    public void RaiseStopped() => ObsStopped?.Invoke();
}

public class PlainProtector : ISecretProtector
{
    public string Protect(string plain) => plain;
    public string Unprotect(string protectedText) => protectedText;
}
