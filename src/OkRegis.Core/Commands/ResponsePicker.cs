using OkRegis.Core.Settings;

namespace OkRegis.Core.Commands;

/// <summary>Choisit au hasard une phrase de réponse et remplace {scène} / {source}.</summary>
public class ResponsePicker
{
    private readonly AppSettings _settings;
    private readonly Random _rng;
    public ResponsePicker(AppSettings settings, Random? rng = null) { _settings = settings; _rng = rng ?? new Random(); }

    public string Pick(string actionKey, string? name)
    {
        if (!_settings.Responses.TryGetValue(actionKey, out var list) || list.Count == 0) return "";
        return list[_rng.Next(list.Count)].Replace("{scène}", name ?? "").Replace("{source}", name ?? "");
    }
}
