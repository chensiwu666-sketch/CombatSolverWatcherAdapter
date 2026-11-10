using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;
using CombatSolver.Engine.InCombat.Simulation;
using WatcherMod;

namespace WatcherAdapter;

/// <summary>
/// Everything a stance change needs from a branch, independent of which mirror context raised it: the
/// card mirror, a turn-start power hook and a turn-end power hook all pass through the same core, so the
/// semantics cannot drift between the card path and the power path.
/// </summary>
internal readonly struct StanceEnv(
    SimulatedCombatState combat,
    CombatPredictionSimulator simulator,
    CombatPredictionHistory history,
    ICombatPredictionEffectSink sink)
{
    internal SimulatedCombatState Combat { get; } = combat;
    internal CombatPredictionSimulator Simulator { get; } = simulator;
    internal CombatPredictionHistory History { get; } = history;
    internal ICombatPredictionEffectSink Sink { get; } = sink;

    internal static StanceEnv From(CardOnPlayMirrorContext context) => Create(context.CombatState, context.Simulator, context.History);

    internal static StanceEnv From(AfterPlayerTurnStartMirrorContext context) => Create(context.CombatState, context.Simulator, context.History);

    internal static StanceEnv From(BeforeSideTurnEndMirrorContext context) => Create(context.CombatState, context.Simulator, context.History);

    internal static StanceEnv FromParts(ICombatState combatState, CombatPredictionSimulator simulator, CombatPredictionHistory history) => Create(combatState, simulator, history);

    private static StanceEnv Create(ICombatState combatState, CombatPredictionSimulator simulator, CombatPredictionHistory history)
    {
        var combat = combatState as SimulatedCombatState
            ?? throw new InvalidOperationException("stance mirror requires a branch combat state (SimulatedCombatState)");
        var sink = combatState as ICombatPredictionEffectSink
            ?? throw new InvalidOperationException("stance mirror requires a writable branch combat state");
        return new StanceEnv(combat, simulator, history, sink);
    }
}

/// <summary>
/// Models the Watcher stance machine.
///
/// The mod's own entry point (WatcherCombatHelper.ChangeStance) is main-thread code that must not run
/// inside the solver's search, so every stance change goes through here:
///   1. refuse when CannotChangeStancePower is present;
///   2. do nothing when the target stance is already active;
///   3. remove the other stances, letting Calm's AfterRemoved pay its 2 energy;
///   4. apply the target stance with one stack (Divinity pays 3 energy on entry);
///   5. run the stance-change listeners that live in the mod helper.
/// </summary>
internal static class StanceOps
{
    internal static readonly Type Calm = typeof(WatcherMod.Calm);
    internal static readonly Type Wrath = typeof(WatcherMod.Wrath);
    internal static readonly Type Divinity = typeof(WatcherMod.Divinity);
    internal static readonly Type Foreseen = typeof(WatcherMod.Foreseen);

    private static readonly Type[] Priority = [Divinity, Foreseen, Wrath, Calm];

    private static readonly MethodInfo AmountCoreMethod = typeof(StanceOps)
        .GetMethod(nameof(AmountCore), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(StanceOps).FullName, nameof(AmountCore));

    internal static void Enter(CardModel card, CardOnPlayMirrorContext context, Type target) =>
        EnterCore(StanceEnv.From(context), card.Owner, card.Owner.Creature, target, card);

    internal static void Exit(CardModel card, CardOnPlayMirrorContext context) =>
        ExitCore(StanceEnv.From(context), card.Owner, card.Owner.Creature);

    internal static void EnterFromEnv(StanceEnv env, Player player, Type target, CardModel? source = null) =>
        EnterCore(env, player, player.Creature, target, source);

    internal static bool IsInStance(StanceEnv env, Creature owner, Type stance) =>
        AmountOf(env, stance, owner) > 0;

    private static void EnterCore(StanceEnv env, Player player, Creature owner, Type target, CardModel source)
    {
        if (env.Combat.GetAmount<CannotChangeStancePower>(owner) > 0)
        {
            return;
        }

        Type current = Current(env, owner);
        if (current == target)
        {
            return;
        }

        RemoveAllStances(env, player, owner);
        env.Sink.ApplyPowerFromSource(target, owner, 1, owner, source);

        // Divinity pays 3 energy on entry (Divinity.AfterApplied). Its automatic exit at end of turn
        // lives in that power's own AfterSideTurnEnd, which the search never runs, so the branch is
        // flagged instead of quietly letting Divinity last forever.
        if (target == Divinity)
        {
            env.Simulator.GainEnergy(player, 3);
            env.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        }

        OnStanceChanged(env, player, owner, current, target);
    }

    private static void ExitCore(StanceEnv env, Player player, Creature owner)
    {
        if (env.Combat.GetAmount<CannotChangeStancePower>(owner) > 0)
        {
            return;
        }

        Type current = Current(env, owner);
        if (current == null)
        {
            return;
        }

        RemoveAllStances(env, player, owner);
        OnStanceChanged(env, player, owner, current, null);
    }

    private static Type Current(StanceEnv env, Creature owner)
    {
        foreach (Type stance in Priority)
        {
            if (AmountOf(env, stance, owner) > 0)
            {
                return stance;
            }
        }

        return null;
    }

    private static int AmountOf(StanceEnv env, Type stance, Creature target) =>
        (int)AmountCoreMethod.MakeGenericMethod(stance).Invoke(null, [env, target]);

    private static int AmountCore<T>(StanceEnv env, Creature target) where T : PowerModel =>
        env.Combat.GetAmount<T>(target);

    private static void RemoveAllStances(StanceEnv env, Player player, Creature owner)
    {
        RemoveOne<Divinity>(env, player, owner);
        RemoveOne<Foreseen>(env, player, owner);
        RemoveOne<Wrath>(env, player, owner);
        RemoveOne<Calm>(env, player, owner);
    }

    private static void RemoveOne<T>(StanceEnv env, Player player, Creature owner) where T : PowerModel
    {
        T power = env.Combat.GetPower<T>(owner);
        if (power == null || power.Amount <= 0)
        {
            return;
        }

        env.Sink.SetPowerAmount(power, 0);

        // Calm pays 2 energy when removed (Calm.AfterRemoved). Third-party code the search must not
        // run, so the effect is reproduced explicitly.
        if (power is Calm)
        {
            env.Simulator.GainEnergy(player, 2);
        }
    }

    private static void OnStanceChanged(StanceEnv env, Player player, Creature owner, Type oldStance, Type newStance)
    {
        // Rushdown: entering Wrath draws its stack count. The mod defers this draw while the play pile is
        // non-empty (RushdownPower.PendingDraws) and pays it later from a hook; the search draws at once.
        // The count is right and the timing is slightly early, so it is reported as a risk.
        if (newStance == Wrath)
        {
            int rushdown = env.Combat.GetAmount<RushdownPower>(owner);
            if (rushdown > 0)
            {
                env.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                env.Simulator.Draw(player, rushdown);
            }
        }

        // MentalFortress: every real stance change gains its stack count as unpowered block.
        if (oldStance != newStance)
        {
            int mentalFortress = env.Combat.GetAmount<MentalFortressPower>(owner);
            if (mentalFortress > 0)
            {
                env.Simulator.GainBlock(owner, mentalFortress, ValueProp.Unpowered);
            }
        }

        // Violet Lotus: +1 energy when leaving Calm (WatcherCombatHelper.OnStanceChanged).
        if (oldStance == Calm && HasVioletLotus(env.Combat, player))
        {
            env.Simulator.GainEnergy(player, 1);
        }

        // Two stance-change listeners are NOT modelled and cannot be detected cheaply from the branch
        // (no discard-pile inspection): FlurryOfBlows returning from the discard, and Foreseen's
        // EnlightenFate. Mark the branch so the overlay reports it.
        env.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
    }

    private static bool HasVioletLotus(SimulatedCombatState combat, Player player)
    {
        try
        {
            foreach (RelicModel relic in combat.RelicsOf(player))
            {
                if (relic != null && relic.Id.Entry == "VIOLET_LOTUS")
                {
                    return true;
                }
            }
        }
        catch
        {
            // Relic lookup is best effort: a miss only costs 1 energy, and the risk marker already
            // tells the player this branch is approximate.
        }

        return false;
    }
}
