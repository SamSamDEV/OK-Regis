using OkRegis.Core.Commands;
using OkRegis.Core.Languages;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;
using OkRegis.Core.Text;

namespace OkRegis.Tests;

public class LanguageTests
{
    private static readonly ObsSnapshot Snap = new(
        new[] { "LoL", "CS2" }, "LoL", new[] { "AFK", "Chat" }, new[] { "Spotify", "Discord", "Mic/Aux" });

    private static ParseResult Parse(Language language, string heard, AppSettings? s = null)
    {
        s ??= new AppSettings();
        s.Language = language;
        return new CommandParser(s).Parse(heard, Snap);
    }

    [Theory]
    [InlineData("regis switch to lol", CommandKind.SwitchScene, "LoL")]
    [InlineData("ok regis go to cs2", CommandKind.SwitchScene, "CS2")]
    [InlineData("hey regis show afk", CommandKind.ShowSource, "AFK")]
    [InlineData("regis hide the chat", CommandKind.HideSource, "Chat")]
    [InlineData("regis mute discord", CommandKind.Mute, "Discord")]
    [InlineData("regis unmute spotify", CommandKind.Unmute, "Spotify")]
    [InlineData("regis mute the mic", CommandKind.Mute, "Mic/Aux")]
    [InlineData("regis lower spotify", CommandKind.VolumeDown, "Spotify")]
    [InlineData("regis raise discord", CommandKind.VolumeUp, "Discord")]
    [InlineData("regis start recording", CommandKind.RecordStart, null)]
    [InlineData("regis stop the recording", CommandKind.RecordStop, null)]
    [InlineData("regis pause recording", CommandKind.RecordPause, null)]
    public void Anglais(string heard, CommandKind kind, string? target)
    {
        var r = Parse(Language.English, heard);
        Assert.Equal(ParseStatus.Ok, r.Status);
        Assert.Equal(kind, r.Command!.Kind);
        Assert.Equal(target, r.Command.Target);
    }

    [Theory]
    [InlineData("regis cambia a lol", CommandKind.SwitchScene, "LoL")]
    [InlineData("oye regis oculta afk", CommandKind.HideSource, "AFK")]
    [InlineData("regis muestra el chat", CommandKind.ShowSource, "Chat")]
    [InlineData("regis silencia discord", CommandKind.Mute, "Discord")]
    [InlineData("regis silencia el micro", CommandKind.Mute, "Mic/Aux")]
    [InlineData("regis sube spotify", CommandKind.VolumeUp, "Spotify")]
    [InlineData("regis baja discord", CommandKind.VolumeDown, "Discord")]
    [InlineData("regis empieza a grabar", CommandKind.RecordStart, null)]
    [InlineData("regis para de grabar", CommandKind.RecordStop, null)]
    [InlineData("regis pausa la grabadora", CommandKind.RecordPause, null)]
    public void Espagnol(string heard, CommandKind kind, string? target)
    {
        var r = Parse(Language.Spanish, heard);
        Assert.Equal(ParseStatus.Ok, r.Status);
        Assert.Equal(kind, r.Command!.Kind);
        Assert.Equal(target, r.Command.Target);
    }

    [Fact]
    public void Les_verbes_d_une_autre_langue_ne_marchent_pas()
    {
        Assert.Equal(ParseStatus.UnknownVerb, Parse(Language.English, "regis mets lol").Status);
        Assert.Equal(ParseStatus.UnknownVerb, Parse(Language.French, "regis switch to lol").Status);
    }

    [Fact]
    public void Le_stream_reste_desactive_par_defaut_dans_toutes_les_langues()
    {
        Assert.Equal(ParseStatus.Disabled, Parse(Language.English, "regis start streaming").Status);
        Assert.Equal(ParseStatus.Disabled, Parse(Language.Spanish, "regis inicia el stream").Status);
    }

    [Fact]
    public void Les_nombres_se_disent_dans_la_langue()
    {
        Assert.Contains("scene twenty one", SpokenForms.For("Scene 21", Language.English));
        Assert.Contains("c s two", SpokenForms.For("CS2", Language.English));
        Assert.Contains("escena veintiuno", SpokenForms.For("Escena 21", Language.Spanish));
        Assert.Contains("treinta y cinco", SpokenForms.For("35", Language.Spanish));
        Assert.Contains("vingt et un", SpokenForms.For("21", Language.French));
    }

    [Fact]
    public void La_grammaire_anglaise_contient_des_phrases_anglaises()
    {
        var vocab = new Vocabulary(new[] { "regis", "ok", "hey", "switch", "to", "mute", "the", "lol", "spotify", "mic", "aux" }, Language.English);
        var s = new AppSettings { Language = Language.English };
        var r = GrammarBuilder.Build(s, Snap, vocab);
        Assert.Contains("regis switch to lol", r.Phrases);
        Assert.Contains("hey regis mute spotify", r.Phrases);
        Assert.Contains("regis mute the mic", r.Phrases);
        Assert.DoesNotContain(r.Phrases, p => p.Contains("mets"));
    }

    [Fact]
    public void Les_reponses_par_defaut_suivent_la_langue()
    {
        var s = new AppSettings();
        Assert.Contains("LoL", new ResponsePicker(s).Pick("scene", "LoL"));
        s.Language = Language.English;
        var english = new ResponsePicker(s).Pick("scene", "LoL");
        Assert.Contains("LoL", english);
        Assert.DoesNotContain("{", english);
        s.Language = Language.Spanish;
        Assert.DoesNotContain("{", new ResponsePicker(s).Pick("show", "AFK"));
    }

    [Fact]
    public void Les_trois_formes_de_variables_sont_remplacees()
    {
        var s = new AppSettings();
        s.Responses["scene"] = new() { "{scène} {scene} {escena} {source} {fuente}" };
        Assert.Equal("X X X X X", new ResponsePicker(s).Pick("scene", "X"));
    }

    [Fact]
    public void Un_nom_d_assistant_proche_d_un_verbe_anglais_est_signale()
    {
        var english = LanguagePack.For(Language.English);
        Assert.NotNull(NameCheck.AssistantNameProblem("show", Array.Empty<string>(), english));
        Assert.Null(NameCheck.AssistantNameProblem("jarvis", Array.Empty<string>(), english));
    }

    [Fact]
    public void Toute_langue_a_des_reponses_pour_toutes_les_actions()
    {
        var keys = new[] { "scene", "show", "hide", "mute", "unmute", "volumeUp", "volumeDown", "recordStart", "recordStop", "recordPause", "streamStart", "streamStop", "failed" };
        foreach (var language in Enum.GetValues<Language>())
            foreach (var key in keys)
                Assert.True(LanguagePack.For(language).DefaultResponses.ContainsKey(key), $"{language}/{key}");
    }

    [Fact]
    public void Les_anciennes_reponses_francaises_par_defaut_sont_oubliees_au_chargement()
    {
        var path = Path.Combine(Path.GetTempPath(), "okregis-lang-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            var old = new AppSettings();
            foreach (var (k, v) in LanguagePack.For(Language.French).DefaultResponses) old.Responses[k] = v.ToList();
            store.Save(old);
            Assert.Empty(store.Load().Responses); // sinon elles resteraient en français après un changement de langue

            var custom = new AppSettings();
            custom.Responses["scene"] = new() { "Ok chef" };
            store.Save(custom);
            Assert.Equal(new[] { "Ok chef" }, store.Load().Responses["scene"]);
        }
        finally { File.Delete(path); }
    }

    // Constaté avec le vrai modèle espagnol : « grabacion » n'était pas retrouvé (trop de variantes d'accents).
    [Fact]
    public void Les_accents_espagnols_sont_retrouves_meme_dans_un_long_mot()
    {
        var model = new HashSet<string> { "grabación", "transmisión", "enseña", "regis" };
        var vocab = new Vocabulary(model.Contains, Language.Spanish);
        Assert.Equal(new[] { "grabación" }, vocab.Resolve("grabacion"));
        Assert.Equal(new[] { "transmisión" }, vocab.Resolve("transmision"));
        Assert.Equal(new[] { "enseña" }, vocab.Resolve("ensena"));
    }

    // « unmute » n'existe pas dans les modèles : « un mute » (deux mots connus) le remplace.
    [Theory]
    [InlineData(Language.French, "regis un mute discord")]
    [InlineData(Language.English, "regis un mute discord")]
    [InlineData(Language.Spanish, "regis un mute discord")]
    public void Un_mute_en_deux_mots_est_compris(Language language, string heard)
        => Assert.Equal(CommandKind.Unmute, Parse(language, heard).Command!.Kind);

    [Fact]
    public void La_langue_se_sauvegarde()
    {
        var path = Path.Combine(Path.GetTempPath(), "okregis-lang2-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { Language = Language.Spanish });
            Assert.Equal(Language.Spanish, store.Load().Language);
            Assert.Equal(Language.French, new AppSettings().Language);
        }
        finally { File.Delete(path); }
    }
}
