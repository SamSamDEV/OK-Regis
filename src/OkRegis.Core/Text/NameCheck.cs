using OkRegis.Core.Languages;
using OkRegis.Core.Localization;

namespace OkRegis.Core.Text;

/// <summary>Vérifie qu'un nom d'assistant ne sera pas confondu avec une commande ou une scène.</summary>
public static class NameCheck
{
    /// <summary>Renvoie un message d'avertissement, ou null si le nom convient.</summary>
    public static string? AssistantNameProblem(string name, IEnumerable<string> obsNames, LanguagePack? pack = null)
    {
        pack ??= LanguagePack.French;
        var n = Normalizer.Normalize(name);
        if (n.Length == 0) return Loc.T("Le nom est vide.");

        foreach (var word in pack.FixedWords)
        {
            var w = Normalizer.Normalize(word);
            if (n == w || (n.Length >= 5 && w.Length >= 5 && Normalizer.Distance(n, w) <= 1))
                return Loc.T("Ce nom ressemble au mot de commande « {0} » : Régis risque de se tromper.", word);
        }
        if (obsNames.Any(o => Normalizer.Normalize(o) == n))
            return Loc.T("Ce nom est aussi celui d'une scène ou d'une source d'OBS.");
        return null;
    }
}
