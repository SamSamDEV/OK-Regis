namespace OkRegis.Core.Commands;

/// <summary>Applique l'ordre choisi par l'utilisateur à une liste de noms venant d'OBS.</summary>
public static class OrderHelper
{
    /// <summary>Les noms de l'ordre enregistré d'abord (ceux qui existent encore), puis les autres dans l'ordre d'OBS.</summary>
    public static IReadOnlyList<string> Apply(IReadOnlyList<string> names, IReadOnlyList<string>? saved)
    {
        if (saved is null || saved.Count == 0) return names;
        var remaining = names.ToList();
        var result = new List<string>(names.Count);
        foreach (var wanted in saved)
        {
            var index = remaining.IndexOf(wanted);
            if (index < 0) continue; // ce nom n'existe plus dans OBS
            result.Add(wanted);
            remaining.RemoveAt(index);
        }
        result.AddRange(remaining); // les nouveaux noms vont à la fin
        return result;
    }
}
