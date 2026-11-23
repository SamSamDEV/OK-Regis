namespace OkRegis.Core.Speech;

/// <summary>Comment est le niveau du micro : trop faible, faible, bon ou trop fort (saturé).</summary>
public enum MicLevel { TooLow, Low, Good, Loud }

/// <summary>
/// Boost logiciel du micro, appliqué UNIQUEMENT à l'audio envoyé à la reconnaissance vocale
/// (OBS n'est pas touché), et mesure du niveau pour aider à régler le micro.
/// </summary>
public static class AudioGain
{
    /// <summary>Amplifie en place des échantillons 16 bits (little-endian) de gainDb décibels, avec écrêtage.</summary>
    public static void Apply(byte[] pcm16, int byteCount, double gainDb)
    {
        if (gainDb <= 0) return;
        var factor = Math.Pow(10, gainDb / 20.0);
        var samples = Math.Min(byteCount, pcm16.Length) / 2;
        for (var i = 0; i < samples; i++)
        {
            var value = BitConverter.ToInt16(pcm16, i * 2) * factor;
            var clipped = (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);
            pcm16[i * 2] = (byte)(clipped & 0xFF);
            pcm16[i * 2 + 1] = (byte)((clipped >> 8) & 0xFF);
        }
    }

    /// <summary>Volume efficace (0-32768) → décibels par rapport au maximum (0 dB = pleine échelle, -120 = silence).</summary>
    public static double LevelDb(double rms) => rms <= 0 ? -120 : Math.Max(-120, 20 * Math.Log10(rms / 32768.0));

    /// <summary>Volume efficace d'une tranche d'audio 16 bits (0-32768).</summary>
    public static double Rms(byte[] pcm16, int byteCount)
    {
        var samples = Math.Min(byteCount, pcm16.Length) / 2;
        if (samples == 0) return 0;
        double sum = 0;
        for (var i = 0; i < samples; i++)
        {
            double s = BitConverter.ToInt16(pcm16, i * 2);
            sum += s * s;
        }
        return Math.Sqrt(sum / samples);
    }

    /// <summary>
    /// Juge le niveau le plus fort mesuré pendant que la personne parle (en dB).
    /// Une voix normale se situe autour de -30 à -10 dB.
    /// </summary>
    public static MicLevel Classify(double speechPeakDb) => speechPeakDb switch
    {
        < -45 => MicLevel.TooLow,
        < -30 => MicLevel.Low,
        <= -6 => MicLevel.Good,
        _ => MicLevel.Loud,
    };
}
