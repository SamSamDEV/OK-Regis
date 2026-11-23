using System.Text.Json;
using OkRegis.Core.Commands;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;
using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class GrammarBuilderTests
{
    private static readonly Vocabulary Vocab = new(new[]
    {
        "régis", "ok", "hey", "mets", "coupe", "affiche", "cache", "arrête", "lance", "le", "l", "stream",
        "enregistrement", "c", "s", "deux", "afk", "lol", "jeu", "micro", "spotify",
    });

    private static ObsSnapshot Snap(params string[] scenes) =>
        new(scenes, null, new[] { "AFK" }, new[] { "Spotify" });

    [Fact]
    public void Phrases_completes_avec_et_sans_ok_hey()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("LoL"), Vocab);
        Assert.Contains("régis mets lol", r.Phrases);
        Assert.Contains("ok régis mets lol", r.Phrases);
        Assert.Contains("hey régis mets lol", r.Phrases);
        Assert.Contains("régis affiche afk", r.Phrases);
        Assert.Contains("régis coupe spotify", r.Phrases);
        Assert.Contains("régis coupe le spotify", r.Phrases);
        Assert.Contains("régis lance l enregistrement", r.Phrases);
        Assert.Contains("[unk]", r.Phrases);
    }

    [Fact]
    public void Chaque_verbe_ne_va_qu_avec_son_type_de_cible()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("LoL"), Vocab);
        Assert.DoesNotContain("régis mets afk", r.Phrases);     // afk est une source, pas une scène
        Assert.DoesNotContain("régis affiche lol", r.Phrases);  // lol est une scène
    }

    [Fact]
    public void Le_stream_est_exclu_tant_qu_il_est_desactive()
    {
        Assert.DoesNotContain("régis arrête le stream", GrammarBuilder.Build(new AppSettings(), Snap("LoL"), Vocab).Phrases);
        var s = new AppSettings();
        s.EnabledCommands[CommandKind.StreamStop] = true;
        Assert.Contains("régis arrête le stream", GrammarBuilder.Build(s, Snap("LoL"), Vocab).Phrases);
    }

    [Fact]
    public void CS2_se_dit_c_s_deux_et_texte_gdi_est_inaudible()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("CS2", "Texte (GDI+) 2"), Vocab);
        Assert.Contains("régis mets c s deux", r.Phrases);
        Assert.Contains("Texte (GDI+) 2", r.Unspeakable);
        Assert.DoesNotContain(r.Phrases, p => p.Contains("texte"));
    }

    [Fact]
    public void Un_surnom_est_propose_pour_la_bonne_cible()
    {
        var s = new AppSettings();
        s.Nicknames["jeu"] = "LoL";
        var r = GrammarBuilder.Build(s, Snap("LoL"), Vocab);
        Assert.Contains("régis mets jeu", r.Phrases);
        Assert.DoesNotContain("régis affiche jeu", r.Phrases);
    }

    [Fact]
    public void Un_surnom_inconnu_du_modele_est_signale()
    {
        var s = new AppSettings();
        s.Nicknames["zzz"] = "LoL";
        Assert.Contains("zzz", GrammarBuilder.Build(s, Snap("LoL"), Vocab).Unspeakable);
    }

    [Fact]
    public void Nom_de_l_assistant_inconnu_ne_laisse_que_unk()
    {
        var r = GrammarBuilder.Build(new AppSettings { AssistantName = "xyzzy" }, Snap("LoL"), Vocab);
        Assert.Contains("xyzzy", r.Unspeakable);
        Assert.Equal(new[] { "[unk]" }, r.Phrases);
    }

    [Fact]
    public void Pas_de_doublons_et_json_valide()
    {
        var r = GrammarBuilder.Build(new AppSettings(), Snap("LoL", "lol"), Vocab);
        Assert.Equal(r.Phrases.Count, r.Phrases.Distinct().Count());
        Assert.Equal(r.Phrases, JsonSerializer.Deserialize<string[]>(GrammarBuilder.ToVoskJson(r)));
    }

    // Sinon « coupe le micro » serait impossible à entendre pour une source nommée "Mic/Aux".
    [Fact]
    public void Micro_et_mute_sont_dans_la_grammaire_pour_Mic_Aux()
    {
        var vocab = new Vocabulary(new[] { "régis", "coupe", "mute", "le", "micro", "mic", "aux", "discord" });
        var snap = new ObsSnapshot(new[] { "LoL" }, "LoL", Array.Empty<string>(), new[] { "Mic/Aux", "Discord" });
        var r = GrammarBuilder.Build(new AppSettings(), snap, vocab);
        Assert.Contains("régis coupe le micro", r.Phrases);
        Assert.Contains("régis coupe mic aux", r.Phrases);
        Assert.Contains("régis mute discord", r.Phrases);
        Assert.Contains("régis coupe aux", r.Phrases);   // un seul mot du nom suffit
    }

    [Fact]
    public void Snapshot_vide_donne_au_moins_les_commandes_fixes()
    {
        var vide = new ObsSnapshot(Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>());
        var r = GrammarBuilder.Build(new AppSettings(), vide, Vocab);
        Assert.Contains("régis lance l enregistrement", r.Phrases);
    }
}
