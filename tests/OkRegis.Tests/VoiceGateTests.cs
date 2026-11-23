using OkRegis.Core.Speech;

namespace OkRegis.Tests;

public class VoiceGateTests
{
    private const int Frame = 100; // ms

    [Fact]
    public void Le_silence_n_est_jamais_analyse()
    {
        var gate = new VoiceGate();
        for (var i = 0; i < 50; i++) Assert.False(gate.Next(60, Frame).Feed);
    }

    [Fact]
    public void La_parole_demarre_l_analyse_et_demande_le_pre_enregistrement()
    {
        var gate = new VoiceGate();
        gate.Next(60, Frame);
        var d = gate.Next(2000, Frame);
        Assert.True(d.Feed); Assert.True(d.Start); Assert.False(d.End);
        Assert.False(gate.Next(2000, Frame).Start); // seulement au début
    }

    [Fact]
    public void L_analyse_continue_pendant_la_traine_puis_se_termine_une_seule_fois()
    {
        var gate = new VoiceGate(hangoverMs: 700);
        gate.Next(2000, Frame);
        var fed = 0; var ends = 0;
        for (var i = 0; i < 12; i++)
        {
            var d = gate.Next(50, Frame);
            if (d.Feed) fed++;
            if (d.End) ends++;
        }
        Assert.Equal(6, fed);   // 700 ms de traîne, par tranches de 100 ms
        Assert.Equal(1, ends);
    }

    [Fact]
    public void Un_bruit_de_fond_constant_ne_declenche_pas_longtemps_puis_la_voix_declenche()
    {
        var gate = new VoiceGate();
        for (var i = 0; i < 300; i++) gate.Next(200, Frame); // souffle régulier : le seuil s'adapte
        Assert.False(gate.Next(200, Frame).Feed);
        Assert.True(gate.Next(3000, Frame).Feed);              // la voix passe toujours
    }

    [Fact]
    public void Un_micro_tres_faible_reste_au_dessus_du_seuil_minimum()
    {
        var gate = new VoiceGate(minThreshold: 150);
        for (var i = 0; i < 100; i++) gate.Next(10, Frame);    // plancher très bas
        Assert.False(gate.Next(140, Frame).Feed);              // sous le minimum
        Assert.True(gate.Next(400, Frame).Feed);               // au-dessus
    }

    [Fact]
    public void Reset_oublie_la_parole_en_cours()
    {
        var gate = new VoiceGate();
        gate.Next(2000, Frame);
        gate.Reset();
        Assert.False(gate.Next(50, Frame).Feed);
    }
}
