using System.Text.Json;
using OkRegis.Core.Commands;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;
using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class GrammarBuilderTests
{
    private static readonly Vocabulary Vocab = new(new[]
        { "régis", "ok", "hey", "mets", "coupe", "affiche", "arrête", "le", "stream", "c", "s", "deux", "afk", "lol" });

    private static ObsSnapshot Snap(params string[] scenes) =>
        new(scenes, null, new[] { "AFK" }, Array.Empty<string>());

    [Fact]
    public void Contient_le_nom_les_verbes_et_unk()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap(), Vocab);
        Assert.Contains("régis", r.Phrases);
        Assert.Contains("mets", r.Phrases);
        Assert.Contains("arrête", r.Phrases);
        Assert.Contains("[unk]", r.Phrases);
    }

    [Fact]
    public void CS2_donne_c_s_deux_et_texte_gdi_est_inaudible()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("CS2", "Texte (GDI+) 2"), Vocab);
        Assert.Contains("c", r.Phrases); Assert.Contains("s", r.Phrases); Assert.Contains("deux", r.Phrases);
        Assert.Contains("Texte (GDI+) 2", r.Unspeakable);
        Assert.DoesNotContain("texte", r.Phrases);
    }

    [Fact]
    public void Nom_de_lassistant_inconnu_est_signale()
    {
        var r = GrammarBuilder.Build(new AppSettings { AssistantName = "xyzzy" }, Snap(), Vocab);
        Assert.Contains("xyzzy", r.Unspeakable);
    }

    [Fact]
    public void Pas_de_doublons_et_json_valide()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("LoL", "lol"), Vocab);
        Assert.Equal(r.Phrases.Count, r.Phrases.Distinct().Count());
        var json = JsonSerializer.Deserialize<string[]>(GrammarBuilder.ToVoskJson(r));
        Assert.Equal(r.Phrases, json);
    }
}
