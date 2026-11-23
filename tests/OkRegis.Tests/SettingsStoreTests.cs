using OkRegis.Core.Settings;

namespace OkRegis.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "okregis-test-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "settings.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Par_defaut_le_stream_est_desactive_et_le_pas_de_volume_vaut_3()
    {
        var s = new AppSettings();
        Assert.False(s.IsEnabled(CommandKind.StreamStart));
        Assert.False(s.IsEnabled(CommandKind.StreamStop));
        Assert.True(s.IsEnabled(CommandKind.SwitchScene));
        Assert.Equal(3.0, s.VolumeStepDb);
    }

    [Fact]
    public void Sauvegarde_puis_chargement_conserve_surnoms_et_accents()
    {
        var store = new SettingsStore(FilePath);
        var s = new AppSettings { AssistantName = "régis" };
        s.Nicknames["jeu"] = "LoL";
        store.Save(s);
        var charge = store.Load();
        Assert.Equal("régis", charge.AssistantName);
        Assert.Equal("LoL", charge.Nicknames["jeu"]);
        Assert.False(charge.IsEnabled(CommandKind.StreamStart));
    }

    [Fact]
    public void Fichier_corrompu_donne_les_valeurs_par_defaut_et_garde_une_copie_bad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ pas du json");
        var charge = new SettingsStore(FilePath).Load();
        Assert.Equal("régis", charge.AssistantName);
        Assert.True(File.Exists(FilePath + ".bad"));
    }

    [Fact]
    public void Fichier_absent_donne_les_valeurs_par_defaut()
        => Assert.False(new SettingsStore(FilePath).Load().FirstRunDone);
}
