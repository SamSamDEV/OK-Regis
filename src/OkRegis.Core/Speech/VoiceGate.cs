namespace OkRegis.Core.Speech;

/// <summary>
/// Détecte quand quelqu'un parle, d'après le volume de chaque tranche d'audio, pour ne faire travailler Vosk
/// que pendant la parole (le silence ne coûte presque plus de processeur).
/// Le seuil s'adapte au bruit de fond (souffle du micro) et ne descend jamais sous un minimum.
/// </summary>
public sealed class VoiceGate
{
    /// <summary>Ce qu'il faut faire de la tranche d'audio qu'on vient de mesurer.</summary>
    public readonly record struct Decision(bool Feed, bool Start, bool End);

    private readonly double _minThreshold, _factor;
    private readonly int _hangoverMs;
    private double _floor = 100;   // niveau de bruit de fond estimé
    private int _hangLeft;         // ms de « traîne » restantes après la dernière tranche forte
    private bool _active;

    /// <param name="minThreshold">volume minimal (0-32768) pour considérer que quelqu'un parle</param>
    /// <param name="factor">le seuil vaut bruit de fond × factor</param>
    /// <param name="hangoverMs">on continue d'analyser un peu après la fin de la parole (fin de phrase, respiration)</param>
    public VoiceGate(double minThreshold = 150, double factor = 2.5, int hangoverMs = 700)
    {
        _minThreshold = minThreshold; _factor = factor; _hangoverMs = hangoverMs;
    }

    /// <param name="rms">volume efficace de la tranche (0-32768)</param>
    /// <param name="frameMs">durée de la tranche</param>
    public Decision Next(double rms, int frameMs)
    {
        var loud = rms > Math.Max(_minThreshold, _floor * _factor);
        var wasActive = _active;
        if (loud) _hangLeft = _hangoverMs;
        else if (_hangLeft > 0) _hangLeft -= frameMs;
        _active = loud || _hangLeft > 0;
        if (!_active) _floor = _floor * 0.95 + rms * 0.05; // le bruit de fond n'est mesuré que pendant le silence
        return new Decision(Feed: _active, Start: _active && !wasActive, End: !_active && wasActive);
    }

    /// <summary>Oublie la parole en cours (ex. quand Régis se met à parler lui-même).</summary>
    public void Reset() { _active = false; _hangLeft = 0; }
}
