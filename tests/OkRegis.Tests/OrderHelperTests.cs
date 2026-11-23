using OkRegis.Core.Commands;
using OkRegis.Core.Settings;

namespace OkRegis.Tests;

public class OrderHelperTests
{
    [Fact]
    public void Lordre_choisi_passe_en_premier_le_reste_garde_lordre_dobs()
        => Assert.Equal(new[] { "C", "A", "B" }, OrderHelper.Apply(new[] { "A", "B", "C" }, new[] { "C", "A" }));

    [Fact]
    public void Les_noms_disparus_sont_ignores_et_les_nouveaux_ajoutes_a_la_fin()
        => Assert.Equal(new[] { "B", "A", "NEW" }, OrderHelper.Apply(new[] { "A", "B", "NEW" }, new[] { "Z", "B", "A" }));

    [Fact]
    public void Sans_ordre_enregistre_rien_ne_change()
    {
        Assert.Equal(new[] { "A", "B" }, OrderHelper.Apply(new[] { "A", "B" }, null));
        Assert.Equal(new[] { "A", "B" }, OrderHelper.Apply(new[] { "A", "B" }, Array.Empty<string>()));
    }

    [Fact]
    public void Les_doublons_ne_sont_ni_perdus_ni_dupliques()
    {
        var result = OrderHelper.Apply(new[] { "AFK", "AFK", "Chat" }, new[] { "Chat", "AFK" });
        Assert.Equal(new[] { "Chat", "AFK", "AFK" }, result);
    }

    [Fact]
    public void Lordre_se_sauvegarde()
    {
        var path = Path.Combine(Path.GetTempPath(), "okregis-order-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            var s = new AppSettings();
            s.SceneOrder = new() { "CS2", "LoL" };
            s.SourceOrder["LoL"] = new() { "Chat", "AFK" };
            store.Save(s);
            var loaded = store.Load();
            Assert.Equal(new[] { "CS2", "LoL" }, loaded.SceneOrder);
            Assert.Equal(new[] { "Chat", "AFK" }, loaded.SourceOrder["LoL"]);
        }
        finally { File.Delete(path); }
    }
}
