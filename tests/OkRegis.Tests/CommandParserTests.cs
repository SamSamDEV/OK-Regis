using OkRegis.Core.Commands;
using OkRegis.Core.Settings;

namespace OkRegis.Tests;

public class CommandParserTests
{
    private static readonly ObsSnapshot Snap = new(
        new[] { "LoL", "CS2", "Minecraft" }, "LoL",
        new[] { "AFK", "FaceReveal" },
        new[] { "Spotify", "Discord", "Micro/Aux" });

    private static ParseResult Parse(string heard, AppSettings? s = null, ObsSnapshot? snap = null)
        => new CommandParser(s ?? new AppSettings()).Parse(heard, snap ?? Snap);

    [Theory]
    [InlineData("ok régis mets lol", CommandKind.SwitchScene, "LoL")]
    [InlineData("régis passe sur cs2", CommandKind.SwitchScene, "CS2")]
    [InlineData("régis affiche afk", CommandKind.ShowSource, "AFK")]
    [InlineData("régis cache afk", CommandKind.HideSource, "AFK")]
    [InlineData("hey régis coupe spotify", CommandKind.Mute, "Spotify")]
    [InlineData("régis coupe le micro", CommandKind.Mute, "Micro/Aux")]
    [InlineData("régis remets discord", CommandKind.Unmute, "Discord")]
    [InlineData("régis monte spotify", CommandKind.VolumeUp, "Spotify")]
    [InlineData("régis baisse discord", CommandKind.VolumeDown, "Discord")]
    [InlineData("régis arrête l enregistrement", CommandKind.RecordStop, null)]
    [InlineData("régis lance l enregistrement", CommandKind.RecordStart, null)]
    [InlineData("régis pause l enregistrement", CommandKind.RecordPause, null)]
    [InlineData("régi mets lol", CommandKind.SwitchScene, "LoL")]
    public void Phrases_comprises(string entendu, CommandKind kind, string? cible)
    {
        var r = Parse(entendu);
        Assert.Equal(ParseStatus.Ok, r.Status);
        Assert.Equal(kind, r.Command!.Kind);
        Assert.Equal(cible, r.Command.Target);
    }

    [Fact] public void Sans_mot_dappel() => Assert.Equal(ParseStatus.NoWakeWord, Parse("mets lol").Status);
    [Fact] public void Verbe_inconnu() => Assert.Equal(ParseStatus.UnknownVerb, Parse("régis fais un café").Status);
    [Fact] public void Cible_inconnue() => Assert.Equal(ParseStatus.UnknownTarget, Parse("régis mets zzz").Status);
    [Fact] public void Stream_desactive_par_defaut() => Assert.Equal(ParseStatus.Disabled, Parse("régis lance le stream").Status);

    [Fact]
    public void Stream_active_dans_les_reglages()
    {
        var s = new AppSettings();
        s.EnabledCommands[CommandKind.StreamStart] = true;
        Assert.Equal(CommandKind.StreamStart, Parse("régis lance le stream", s).Command!.Kind);
    }

    [Fact]
    public void Surnom_resolu()
    {
        var s = new AppSettings();
        s.Nicknames["jeu"] = "LoL";
        Assert.Equal("LoL", Parse("régis mets jeu", s).Command!.Target);
    }

    [Fact]
    public void Doublons_le_premier_gagne_sans_exception()
    {
        var snap = new ObsSnapshot(new[] { "LoL" }, "LoL", new[] { "AFK", "afk" }, Array.Empty<string>());
        Assert.Equal("AFK", Parse("régis affiche afk", null, snap).Command!.Target);
    }

    // Revue n°2 : Vosk n'entend que "c s deux", pas "CS2".
    [Fact]
    public void Noms_dits_en_formes_parlees_sont_compris()
    {
        Assert.Equal("CS2", Parse("régis mets c s deux").Command!.Target);
        var s21 = new ObsSnapshot(new[] { "Scene 21" }, "Scene 21", Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("Scene 21", Parse("régis mets scene vingt et un", null, s21).Command!.Target);
        var afk = new ObsSnapshot(new[] { "LoL" }, "LoL", new[] { "AFK" }, Array.Empty<string>());
        Assert.Equal("AFK", Parse("régis affiche a f k", null, afk).Command!.Target);
    }

    // « coupe le micro » doit marcher même si la source s'appelle "Mic/Aux" ; « mute » / « unmute » aussi.
    [Fact]
    public void Micro_est_un_alias_de_Mic_Aux()
    {
        var snap = new ObsSnapshot(new[] { "LoL" }, "LoL", Array.Empty<string>(), new[] { "Desktop Audio", "Mic/Aux", "Discord" });
        Assert.Equal("Mic/Aux", Parse("régis coupe le micro", null, snap).Command!.Target);
        Assert.Equal("Mic/Aux", Parse("régis remets le micro", null, snap).Command!.Target);
    }

    [Theory]
    [InlineData("régis mute discord", CommandKind.Mute)]
    [InlineData("régis unmute discord", CommandKind.Unmute)]
    [InlineData("régis muet spotify", CommandKind.Mute)]
    public void Mute_et_unmute_sont_compris(string entendu, CommandKind kind)
    {
        var r = Parse(entendu);
        Assert.Equal(ParseStatus.Ok, r.Status);
        Assert.Equal(kind, r.Command!.Kind);
    }

    [Fact]
    public void Un_mot_dun_nom_suffit_pour_la_cible()
    {
        var snap = new ObsSnapshot(new[] { "Just Chatting", "LoL" }, "LoL", Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("Just Chatting", Parse("régis mets chatting", null, snap).Command!.Target);
    }

    [Fact]
    public void Nom_de_lassistant_avec_chiffre_peut_reveiller()
    {
        var s = new AppSettings { AssistantName = "R2" };
        Assert.Equal(ParseStatus.Ok, Parse("ok r deux mets lol", s).Status);
    }

    [Fact]
    public void Snapshot_vide_donne_cible_inconnue()
    {
        var vide = new ObsSnapshot(Array.Empty<string>(), null, Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal(ParseStatus.UnknownTarget, Parse("régis mets lol", null, vide).Status);
    }

    [Fact]
    public void Nom_de_scene_compose_de_symboles_ne_plante_pas()
    {
        var snap = new ObsSnapshot(new[] { "🎮", "Écran Été" }, "🎮", Array.Empty<string>(), Array.Empty<string>());
        Assert.Equal("Écran Été", Parse("régis mets écran été", null, snap).Command!.Target);
        Assert.Equal(ParseStatus.UnknownTarget, Parse("régis mets", null, snap).Status);
    }
}
