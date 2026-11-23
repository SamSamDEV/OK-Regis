using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class SpokenFormsTests
{
    private static Vocabulary Vocab(params string[] mots) => new(mots);

    [Fact]
    public void CS2_se_dit_c_s_deux()
    {
        var ok = Vocab("c", "s", "deux", "mets", "afk").CanSpeak("CS2", out var forme);
        Assert.True(ok);
        Assert.Equal("c s deux", forme);
    }

    [Fact]
    public void Nom_inconnu_est_non_prononcable()
    {
        var v = Vocab("c", "s", "deux", "mets", "afk");
        Assert.False(v.CanSpeak("Texte (GDI+) 2", out _));
        Assert.False(v.CanSpeak("🎮", out _));
    }

    [Fact]
    public void Les_chiffres_sont_ecrits_en_lettres()
    {
        Assert.Contains("vingt et un", SpokenForms.For("21"));
        Assert.Contains("soixante et onze", SpokenForms.For("71"));
    }

    [Fact]
    public void Les_accents_du_modele_sont_retrouves()
    {
        var v = Vocab("régis", "mets");
        Assert.True(v.CanSpeak("Regis", out var forme));
        Assert.Equal(new[] { "régis" }, v.Resolve(forme!));
    }
}
