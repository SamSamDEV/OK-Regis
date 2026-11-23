using OkRegis.Core.Languages;
using OkRegis.Core.Settings;

namespace OkRegis.Core.Commands;

/// <summary>Choisit au hasard une phrase de réponse (personnalisée, sinon celle de la langue) et remplace les variables.</summary>
public class ResponsePicker
{
    private readonly AppSettings _settings;
    private readonly Random _rng;
    public ResponsePicker(AppSettings settings, Random? rng = null) { _settings = settings; _rng = rng ?? new Random(); }

    /// <summary>Les phrases possibles pour une action : celles de l'utilisateur, sinon celles par défaut de la langue.</summary>
    public IReadOnlyList<string> PhrasesFor(string actionKey)
    {
        if (_settings.Responses.TryGetValue(actionKey, out var custom) && custom.Count > 0) return custom;
        return LanguagePack.For(_settings.Language).DefaultResponses.TryGetValue(actionKey, out var defaults) ? defaults : Array.Empty<string>();
    }

    public string Pick(string actionKey, string? name)
    {
        var list = PhrasesFor(actionKey);
        if (list.Count == 0) return "";
        var n = name ?? "";
        // Les variables s'écrivent dans la langue de l'utilisateur ; toutes sont acceptées.
        return list[_rng.Next(list.Count)]
            .Replace("{scène}", n).Replace("{scene}", n).Replace("{escena}", n)
            .Replace("{source}", n).Replace("{fuente}", n);
    }
}
