using OkRegis.Core.Settings;
using OkRegis.Core.Speech;

namespace OkRegis.Tests;

public class AudioGainTests
{
    private static byte[] Pcm(params short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static short[] Samples(byte[] bytes)
    {
        var s = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, s, 0, bytes.Length);
        return s;
    }

    [Fact]
    public void Sans_boost_le_son_ne_change_pas()
    {
        var pcm = Pcm(100, -200, 300);
        AudioGain.Apply(pcm, pcm.Length, 0);
        Assert.Equal(new short[] { 100, -200, 300 }, Samples(pcm));
    }

    [Fact]
    public void Six_decibels_doublent_environ_l_amplitude()
    {
        var pcm = Pcm(1000, -1000);
        AudioGain.Apply(pcm, pcm.Length, 6.0206);
        var s = Samples(pcm);
        Assert.InRange(s[0], 1995, 2005);
        Assert.InRange(s[1], -2005, -1995);
    }

    [Fact]
    public void Le_son_trop_fort_est_ecrete_sans_deborder()
    {
        var pcm = Pcm(30000, -30000, short.MaxValue, short.MinValue);
        AudioGain.Apply(pcm, pcm.Length, 20);
        Assert.Equal(new short[] { short.MaxValue, short.MinValue, short.MaxValue, short.MinValue }, Samples(pcm));
    }

    [Fact]
    public void Seuls_les_octets_demandes_sont_modifies()
    {
        var pcm = Pcm(1000, 1000, 1000);
        AudioGain.Apply(pcm, 4, 6.0206); // 2 échantillons sur 3
        var s = Samples(pcm);
        Assert.InRange(s[0], 1995, 2005);
        Assert.Equal(1000, s[2]);
    }

    [Theory]
    [InlineData(0, -120)]
    [InlineData(32768, 0)]
    [InlineData(16384, -6)]
    public void Niveau_en_dB_par_rapport_au_maximum(double rms, double expected)
        => Assert.Equal(expected, AudioGain.LevelDb(rms), 0);

    [Fact]
    public void Le_niveau_dit_si_le_micro_est_trop_faible_correct_ou_sature()
    {
        Assert.Equal(MicLevel.TooLow, AudioGain.Classify(-52));
        Assert.Equal(MicLevel.Low, AudioGain.Classify(-38));
        Assert.Equal(MicLevel.Good, AudioGain.Classify(-22));
        Assert.Equal(MicLevel.Loud, AudioGain.Classify(-2));
    }

    [Fact]
    public void Le_boost_par_defaut_est_nul_et_se_sauvegarde()
    {
        Assert.Equal(0, new AppSettings().MicGainDb);
        var path = Path.Combine(Path.GetTempPath(), "okregis-gain-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { MicGainDb = 12 });
            Assert.Equal(12, store.Load().MicGainDb);
        }
        finally { File.Delete(path); }
    }
}
