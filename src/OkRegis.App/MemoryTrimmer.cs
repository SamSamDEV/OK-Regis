using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace OkRegis.App;

/// <summary>
/// Rend la mémoire inutilisée à Windows quand l'appli est au repos : ramasse-miettes complet, puis on vide la
/// « mémoire de travail » (les pages réellement utilisées reviennent toutes seules, en douceur, quand on en a besoin).
/// </summary>
public static class MemoryTrimmer
{
    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr process);

    private static readonly System.Threading.Timer Timer = new(_ => Trim(), null, Timeout.Infinite, Timeout.Infinite);

    /// <summary>Programme un nettoyage dans delayMs ; chaque nouvel appel repousse l'échéance (un seul minuteur, pas de boucle).</summary>
    public static void Schedule(int delayMs) => Timer.Change(delayMs, Timeout.Infinite);

    public static void Trim()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            using var me = Process.GetCurrentProcess();
            EmptyWorkingSet(me.Handle);
        }
        catch (Exception) { /* un nettoyage raté n'a aucune importance */ }
    }
}
