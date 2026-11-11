using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using WatcherMod;

namespace WatcherAdapter;

/// <summary>
/// Mantra accumulation.
///
/// The mod's <c>WatcherCombatHelper.GainMantra</c> adds the Mantra power and <c>Mantra.AfterPowerAmountChangedCompat</c>
/// converts every full 10 into Divinity. That conversion is third-party code the search never runs, so
/// it is reproduced here — otherwise Mantra would sit at 10+ forever and Divinity would never happen.
/// </summary>
internal static class MantraOps
{
    internal static void Gain(StanceEnv env, Player player, int amount, CardModel? source)
    {
        if (amount <= 0)
        {
            return;
        }

        // The mod's GainMantra runs inside WatcherStatePower: it ensures the invisible state power
        // exists and bumps its per-combat / per-turn counters before adding the Mantra power. Those
        // counters are what 光辉 (Brilliance) and 福音 (Gospel) read, so they have to be maintained
        // here — third-party field writes never happen inside the search.
        var state = env.Combat.GetPower<WatcherMod.WatcherStatePower>(player.Creature);
        if (state == null)
        {
            env.Sink.ApplyPowerFromSource(
                typeof(WatcherMod.WatcherStatePower), player.Creature, 1, player.Creature, source);
            state = env.Combat.GetPower<WatcherMod.WatcherStatePower>(player.Creature);
        }

        if (state != null)
        {
            state.TotalMantraGainedThisCombat += amount;
            state.MantraGainedThisTurn += amount;
        }

        env.Sink.ApplyPowerFromSource(typeof(Mantra), player.Creature, amount, player.Creature, source);

        int mantra = env.Combat.GetAmount<Mantra>(player.Creature);
        while (mantra >= 10)
        {
            env.Sink.SetPowerAmount(env.Combat.GetPower<Mantra>(player.Creature), mantra - 10);
            StanceOps.EnterFromEnv(env, player, StanceOps.Divinity, source);
            mantra -= 10;
        }
    }
}
