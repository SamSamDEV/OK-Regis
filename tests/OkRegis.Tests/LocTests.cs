using System.Text.RegularExpressions;
using OkRegis.Core.Localization;
using OkRegis.Core.Settings;

namespace OkRegis.Tests;

public class LocTests
{
    // Le dossier du dépôt : on remonte jusqu'à trouver le dossier "src".
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "OkRegis.Core"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("dépôt introuvable");
    }

    // Les textes écrits dans le code sous la forme Loc.T("...").
    private static IEnumerable<string> LiteralsInCode()
    {
        var regex = new Regex("Loc\\.T\\(\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith("Loc.cs")) continue;
            foreach (Match m in regex.Matches(File.ReadAllText(file)))
                yield return m.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }

    [Fact]
    public void Tous_les_textes_du_code_ont_une_traduction()
    {
        var missing = LiteralsInCode().Distinct().Where(k => !Loc.Keys.Contains(k)).ToList();
        Assert.True(missing.Count == 0, "Textes sans traduction : " + string.Join(" | ", missing));
    }

    [Fact]
    public void Chaque_traduction_est_remplie_et_garde_les_memes_variables()
    {
        foreach (var key in Loc.Keys)
        {
            var en = Loc.Translate(key, Language.English);
            var es = Loc.Translate(key, Language.Spanish);
            Assert.False(string.IsNullOrWhiteSpace(en), "EN vide : " + key);
            Assert.False(string.IsNullOrWhiteSpace(es), "ES vide : " + key);
            Assert.Equal(Placeholders(key), Placeholders(en));
            Assert.Equal(Placeholders(key), Placeholders(es));
            Assert.Equal(key.Contains('\n'), en.Contains('\n')); // les retours à la ligne sont conservés
        }
    }

    private static string Placeholders(string text) =>
        string.Join(",", Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x));

    [Fact]
    public void Un_texte_inconnu_reste_en_francais()
        => Assert.Equal("Texte pas dans la table", Loc.Translate("Texte pas dans la table", Language.English));

    [Fact]
    public void Les_traductions_changent_vraiment_le_texte()
    {
        Assert.Equal("Settings", Loc.Translate("Réglages", Language.English));
        Assert.Equal("Ajustes", Loc.Translate("Réglages", Language.Spanish));
        Assert.Equal("Réglages", Loc.Translate("Réglages", Language.French));
    }

    [Fact]
    public void Les_noms_de_langue_sont_ecrits_dans_leur_langue()
    {
        Assert.Equal("Français", Loc.NameOf(Language.French));
        Assert.Equal("English", Loc.NameOf(Language.English));
        Assert.Equal("Español", Loc.NameOf(Language.Spanish));
    }
}
