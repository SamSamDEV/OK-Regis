namespace OkRegis.Core.Commands;

/// <summary>Photo de ce qu'OBS contient à un instant donné.</summary>
public record ObsSnapshot(
    IReadOnlyList<string> Scenes,
    string? CurrentScene,
    IReadOnlyList<string> SourcesInCurrentScene,
    IReadOnlyList<string> AudioSources,
    /// <summary>Les sources de CHAQUE scène (groupes dépliés), comme dans OBS.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>>? SourcesByScene = null,
    /// <summary>Pour chaque source audio : vrai si elle est coupée (muet).</summary>
    IReadOnlyDictionary<string, bool>? AudioMuted = null);
