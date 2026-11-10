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
