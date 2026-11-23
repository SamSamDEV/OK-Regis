namespace OkRegis.Core.Speech;

/// <summary>Une phrase entendue et la confiance de Vosk (0 à 1).</summary>
public record Heard(string Text, double Confidence);

/// <summary>Écoute le micro. La vraie version (NAudio + Vosk) est dans l'appli.</summary>
public interface IMicListener : IDisposable
{
    event Action<Heard> PhraseHeard;
    /// <summary>Ouvre le micro (null = micro par défaut).</summary>
    void Start(string? micDeviceName);
    /// <summary>Vrai si le micro est vraiment ouvert (faux si le micro ou le modèle est indisponible).</summary>
    bool IsRunning { get; }
    /// <summary>Ferme le micro.</summary>
    void Stop();
    /// <summary>Change la liste de mots reconnus, sans fermer le micro.</summary>
    void SetGrammar(string voskJsonGrammar);
    /// <summary>Vrai pendant que Régis parle : tout ce qui est capté est jeté.</summary>
    bool Muted { get; set; }
}
