using OkRegis.Core.Commands;
using OkRegis.Core.Settings;

namespace OkRegis.Tests;

public class CommandExecutorTests
{
    private static readonly ObsSnapshot Snap = new(new[] { "LoL" }, "LoL", new[] { "AFK" }, new[] { "Discord" });

    [Fact]
    public async Task Change_de_scene()
    {
        var obs = new FakeObsClient();
        var r = await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.SwitchScene, "LoL"), Snap);
        Assert.Equal(new[] { "scene LoL" }, obs.Calls);
        Assert.True(r.Success); Assert.Equal("scene", r.ActionKey); Assert.Equal("LoL", r.Name);
    }

    [Fact]
    public async Task Affiche_dans_la_scene_courante()
    {
        var obs = new FakeObsClient();
        await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.ShowSource, "AFK"), Snap);
        Assert.Equal(new[] { "visible LoL/AFK=True" }, obs.Calls);
    }

    [Fact]
    public async Task Baisse_le_volume_du_pas_reglable()
    {
        var obs = new FakeObsClient();
        await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.VolumeDown, "Discord"), Snap);
        Assert.Equal(new[] { "volume Discord-3.0" }, obs.Calls);
    }

    [Fact]
    public async Task Commande_desactivee_ne_touche_pas_obs()
    {
        var obs = new FakeObsClient();
        var r = await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.StreamStart, null), Snap);
        Assert.Empty(obs.Calls); Assert.False(r.Success);
    }

    [Fact]
    public async Task Panne_obs_donne_un_echec_sans_exception()
    {
        var obs = new FakeObsClient { FailWith = new InvalidOperationException("OBS fermé") };
        var r = await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.Mute, "Discord"), Snap);
        Assert.False(r.Success); Assert.Equal("failed", r.ActionKey);
    }

    [Fact]
    public async Task Sans_scene_courante_afficher_echoue()
    {
        var obs = new FakeObsClient();
        var snap = Snap with { CurrentScene = null };
        var r = await new CommandExecutor(obs, new AppSettings()).ExecuteAsync(new Command(CommandKind.ShowSource, "AFK"), snap);
        Assert.False(r.Success); Assert.Empty(obs.Calls);
    }

    [Fact]
    public void ResponsePicker_remplace_les_variables()
    {
        var s = new AppSettings();
        s.Responses["scene"] = new() { "Scène {scène}" };
        Assert.Equal("Scène LoL", new ResponsePicker(s).Pick("scene", "LoL"));
        Assert.Equal("", new ResponsePicker(s).Pick("inconnu", null));
    }
}
