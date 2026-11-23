using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class NameCheckTests
{
    [Theory]
    [InlineData("mets")]
    [InlineData("Coupe")]
    [InlineData("affiche")]
    [InlineData("afiche")]   // petite faute d'un verbe
    public void Un_nom_qui_ressemble_a_un_verbe_est_signale(string nom)
        => Assert.NotNull(NameCheck.AssistantNameProblem(nom, Array.Empty<string>()));

    [Fact]
    public void Un_nom_egal_a_une_scene_est_signale()
        => Assert.NotNull(NameCheck.AssistantNameProblem("LoL", new[] { "LoL", "CS2" }));

    [Theory]
    [InlineData("régis")]
    [InlineData("bob")]
    [InlineData("jarvis")]
    public void Un_nom_normal_passe(string nom)
        => Assert.Null(NameCheck.AssistantNameProblem(nom, new[] { "LoL" }));
}
