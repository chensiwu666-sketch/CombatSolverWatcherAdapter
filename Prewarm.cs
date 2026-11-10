using System;
using CombatSolver;

namespace WatcherAdapter;

/// <summary>
/// Declares the Watcher powers whose canonical instance may be materialised on the main thread.
///
/// The solver forbids lazily creating a Power's display dynamic vars on the search thread: a power that
/// is applied for the first time during the search has to be materialised while the root is captured.
/// Its warm-up only covers vanilla sources, so a third-party power that can be applied for the first
/// time inside the search must be declared here — that is the whole reason the API exists
/// (docs/third-party/README.md §1).
///
/// Symptom when a power is missing from this list: every route that applies it dies with
/// "后台模拟尝试惰性创建 Power 显示变量：power=WRATH" and the panel shows 计算失败 rather than a route.
/// </summary>
internal static class Prewarm
{
    private static readonly Type[] AdaptedPowers =
    [
        // stances
        typeof(WatcherMod.Calm),
        typeof(WatcherMod.Wrath),
        typeof(WatcherMod.Divinity),
        typeof(WatcherMod.Foreseen),

        // powers applied by the mirrored cards
        typeof(WatcherMod.ColdObservationPower),
        typeof(WatcherMod.CollectPower),
        typeof(WatcherMod.DevaPower),
        typeof(WatcherMod.EstablishmentPower),
        typeof(WatcherMod.ForesightPower),
        typeof(WatcherMod.LikeWaterPower),
        typeof(WatcherMod.MasterRealityPower),
        typeof(WatcherMod.MentalFortressPower),
        typeof(WatcherMod.NirvanaPower),
        typeof(WatcherMod.OmegaPower),
        typeof(WatcherMod.RushdownPower),
        typeof(WatcherMod.SimmeringFuryPower),
        typeof(WatcherMod.StudyPower),
        typeof(WatcherMod.WaveOfTheHandPower),
        typeof(WatcherMod.EnergyDownPower),
        typeof(WatcherMod.DevotionPower),
        typeof(WatcherMod.GospelPower),
        typeof(WatcherMod.BlockReturnPower),
        typeof(WatcherMod.MarkPower),
        typeof(WatcherMod.EndTurnDeathPower),
        typeof(WatcherMod.BattleHymnPower),
    ];

    internal static void RegisterAll()
    {
        int ok = 0;
        int failed = 0;

        foreach (Type powerType in AdaptedPowers)
        {
            try
            {
                PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower(powerType);
                ok++;
            }
            catch (Exception ex)
            {
                failed++;
                Entry.Log($"FAILED to prewarm {powerType.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Entry.Log($"canonical power warm-up declared: {ok} ok, {failed} failed");
    }
}
