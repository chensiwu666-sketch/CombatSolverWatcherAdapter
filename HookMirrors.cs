using System;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Damage;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;

namespace WatcherAdapter;

/// <summary>
/// Power hook mirrors for the Watcher's stance powers.
///
/// These powers override ordinary game hooks, and third-party listeners do not reach the search on
/// their own: an unregistered listener is reported as a risk and its effect simply never happens. For
/// the stances that is not acceptable — Wrath doubling / Divinity tripling every move-flagged damage
/// number is the single biggest number in a Watcher fight, so it must be registered.
///
/// Wrath doubles damage dealt by its owner AND damage taken by its owner; Divinity only triples damage
/// the owner deals. Both apply only to move damage that is not Unpowered, exactly like the mod.
/// </summary>
internal static class HookMirrors
{
    internal static void RegisterAll()
    {
        RegisterStance<WatcherMod.Wrath>(2m, doublesIncoming: true);
        RegisterStance<WatcherMod.Divinity>(3m, doublesIncoming: false);
        RegisterExtraTurn();
    }

    /// <summary>
    /// 腾跃's extra turn. The mod grants it by applying WatcherExtraTurnPower and ending the turn; the
    /// power's ShouldTakeExtraTurn answers true for its owner, and AfterTakingExtraTurn removes it.
    /// Both hooks are third-party code the search never runs, so without this registration the card
    /// would look like a plain "discard your turn" and the search would refuse to play it.
    /// </summary>
    private static void RegisterExtraTurn()
    {
        try
        {
            ExtraTurnMirrors.RegisterShouldTakeExtraTurn<WatcherMod.WatcherExtraTurnPower>(
                (power, context) => context.Player == power.Owner.Player);
            ExtraTurnMirrors.RegisterAfterTakingExtraTurn<WatcherMod.WatcherExtraTurnPower>(
                (power, context) =>
                {
                    if (context.Player != power.Owner.Player)
                    {
                        return;
                    }

                    context.Combat.SetPowerAmount(power, 0);
                });
            Entry.Log("hook mirror registered: WatcherExtraTurnPower extra turn");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register WatcherExtraTurnPower extra turn: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RegisterStance<T>(decimal multiplier, bool doublesIncoming) where T : PowerModel
    {
        try
        {
            ModifyDamageMirrors.MultiplicativeRegistry.Register<T>((power, context) =>
                Multiplier(power, context, multiplier, doublesIncoming));
            Entry.Log($"hook mirror registered: {typeof(T).Name} x{multiplier}");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register {typeof(T).Name} damage multiplier: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    private static decimal Multiplier(
        PowerModel power,
        ModifyDamageMirrorContext context,
        decimal multiplier,
        bool doublesIncoming)
    {
        // A stance only multiplies while it is actually applied; zero-stack powers can still appear in
        // the listener list and must behave like the mod (which returns 1 for them).
        if (power.Amount <= 0)
        {
            return 1m;
        }

        bool move = context.Props.HasFlag(ValueProp.Move) && !context.Props.HasFlag(ValueProp.Unpowered);
        if (!move)
        {
            return 1m;
        }

        if (context.Dealer == power.Owner)
        {
            return multiplier;
        }

        return doublesIncoming && context.Target == power.Owner ? multiplier : 1m;
    }
}
