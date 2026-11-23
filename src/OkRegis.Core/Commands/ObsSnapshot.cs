namespace OkRegis.Core.Commands;

/// <summary>Photo de ce qu'OBS contient à un instant donné.</summary>
public record ObsSnapshot(
    IReadOnlyList<string> Scenes,
    string? CurrentScene,
    IReadOnlyList<string> SourcesInCurrentScene,
    IReadOnlyList<string> AudioSources);
