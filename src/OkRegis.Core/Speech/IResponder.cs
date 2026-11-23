namespace OkRegis.Core.Speech;

/// <summary>Fait répondre Régis (voix, fichier son, bip ou silence).</summary>
public interface IResponder
{
    event Action SpeakingStarted;
    event Action SpeakingEnded;
    Task RespondAsync(string actionKey, string text);
}
