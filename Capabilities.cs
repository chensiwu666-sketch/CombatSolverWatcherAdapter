using System;
using System.Reflection;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Probes for solver capabilities this adapter can use only when they exist.
///
/// The regular AfterSideTurnEnd registration entry is upstreamed but not released yet (PR #245). Until a
/// solver build contains it, Study and Omega stay unmodelled; the moment a build does contain it, the log
/// says so and the wiring in <see cref="PowerMirrors"/> can be switched on. The probe is deliberately
/// name-based: the adapter must not take a compile-time dependency on an API that is not in the shipped mod.
/// </summary>
internal static class Capabilities
{
    private const string SideTurnEndMirrorsType =
        "CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd.AfterSideTurnEndMirrors";

    internal static bool HasRegularSideTurnEndRegistration { get; private set; }

    internal static void Probe()
    {
        Assembly solver = typeof(CardOnPlayMirrors).Assembly;
        string version = solver.GetName().Version?.ToString() ?? "<unknown>";

        Type? mirrors = solver.GetType(SideTurnEndMirrorsType, throwOnError: false);
        HasRegularSideTurnEndRegistration = mirrors != null;

        Entry.Log(HasRegularSideTurnEndRegistration
            ? $"capability: regular AfterSideTurnEnd registration is available (solver {version}) - Study/Omega can be enabled"
            : $"capability: regular AfterSideTurnEnd registration is absent (solver {version}) - Study/Omega stay unmodelled");
    }
}
