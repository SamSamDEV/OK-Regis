using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class NormalizerTests
{
    [Theory]
    [InlineData("Écran Été", "ecran ete")]
    [InlineData("Texte (GDI+) 2", "texte gdi 2")]
    [InlineData("🎮", "")]
    [InlineData("  A   FK ", "a fk")]
    public void Normalize_nettoie_le_texte(string entree, string attendu)
        => Assert.Equal(attendu, Normalizer.Normalize(entree));

    [Fact]
    public void IsClose_tolere_les_petites_fautes()
    {
        Assert.True(Normalizer.IsClose("spotify", "spotifi"));
        Assert.True(Normalizer.IsClose("lol", "lal"));
        Assert.False(Normalizer.IsClose("lol", "bob"));
    }
}
