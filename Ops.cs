using System;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;

namespace WatcherAdapter;

/// <summary>
/// Small vocabulary shared by every Watcher card mirror. Each method models exactly one game command,
/// so a card mirror reads as the same sequence of lines as the mod's OnPlay body.
///
/// IMPORTANT: every method takes the <c>card</c> the registry handed to the handler. CardOnPlayMirrors
/// dispatches with <c>card.MutablePreview</c> - the instance that belongs to the branch being simulated.
/// <c>context.OriginalCard</c> is the LIVE card: reading its dynamic vars or passing it into a command
/// runs against real combat state from a background thread. That does not throw, it just produces
/// silently wrong numbers (observed: every attack mirrored through the live card dealt 0), which is
/// exactly the "looks fine, computes garbage" failure this adapter must avoid.
/// </summary>
internal static class Ops
{
    internal static bool Pending(CardOnPlayMirrorContext context) => context.Simulator.HasPendingChoice;

    /// <summary>Solver-provided general attack mirror (reads Damage/CalculatedDamage, hits, target type).</summary>
    internal static void Attack(CardModel card, CardOnPlayMirrorContext context) =>
        GeneralCardMirrors.GeneralAttackOnPlay(card, context);

    /// <summary>Solver-provided general block mirror (reads the Block dynamic var).</summary>
    internal static void Block(CardModel card, CardOnPlayMirrorContext context) =>
        GeneralCardMirrors.GeneralBlockOnPlay(card, context);

    /// <summary>Solver-provided general draw mirror (reads the Cards dynamic var).</summary>
    internal static void Draw(CardModel card, CardOnPlayMirrorContext context) =>
        GeneralCardMirrors.GeneralOwnerDrawOnPlay(card, context);

    internal static void Draw(CardModel card, CardOnPlayMirrorContext context, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        context.Simulator.Draw(card.Owner, amount);
    }

    internal static void AttackSingle(CardOnPlayMirrorContext context, int hits)
    {
        if (hits <= 0)
        {
            return;
        }

        context.AttackSingle(hits);
    }

    internal static void AttackRandom(CardOnPlayMirrorContext context, int hits)
    {
        if (hits <= 0)
        {
            return;
        }

        context.AttackRandomOpponents(hits);
    }

    internal static void AttackAll(CardOnPlayMirrorContext context, int hits)
    {
        if (hits <= 0)
        {
            return;
        }

        context.AttackAllOpponents(hits);
    }

    internal static void BlockFor(CardOnPlayMirrorContext context, Creature target, int amount, ValueProp props = ValueProp.Unpowered)
    {
        if (amount <= 0)
        {
            return;
        }

        context.GainBlock(target, amount, props);
    }

    internal static ICombatPredictionEffectSink Sink(CardOnPlayMirrorContext context) =>
        context.CombatState as ICombatPredictionEffectSink
        ?? throw new InvalidOperationException("predicted combat state is not writable");

    /// <summary>Applies a power exactly like the mod's WatcherPowerCmdCompat.Apply: attributed to this card.</summary>
    internal static void Power(CardModel card, CardOnPlayMirrorContext context, Type powerType, Creature target, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Sink(context).ApplyPowerFromSource(powerType, target, amount, card.Owner.Creature, card);
    }

    internal static void PowerSelf(CardModel card, CardOnPlayMirrorContext context, Type powerType, int amount) =>
        Power(card, context, powerType, card.Owner.Creature, amount);

    /// <summary>Reads a dynamic var off the branch card, correction-aware like the solver's own mirrors.</summary>
    internal static int Var(CardModel card, CardOnPlayMirrorContext context, string key)
    {
        if (!card.DynamicVars.TryGetValue(key, out var dynamicVar))
        {
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            EngineDiagnostics.Warn($"[WatcherAdapter] card {card.Id} has no dynamic var '{key}'.");
            return 0;
        }

        return (int)context.Calculate(dynamicVar);
    }
}
