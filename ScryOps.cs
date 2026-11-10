using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using CombatSolver;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using WatcherMod;

namespace WatcherAdapter;

/// <summary>
/// Scry (look at the top N of the draw pile and discard some of them).
///
/// The solver has no notion of scry: its action enumeration covers card plays, targets and potions, so a
/// "which cards do I discard" decision cannot become a search branch. What CAN be done exactly is the
/// state transition — peek the top N, discard a chosen subset, leave the rest in place.
///
/// So the discard choice is made here with a fixed rule (discard only cards the solver itself values
/// below zero, i.e. curses and statuses) and the branch is flagged. That keeps the transition legal and
/// never fabricates a discard of a card the player would want, at the cost of understating scry's value.
/// </summary>
internal static class ScryOps
{
    internal static void Do(CardOnPlayMirrorContext context, Player player, int amount, CardModel source)
    {
        if (amount <= 0)
        {
            return;
        }

        var state = context.State.GetPlayerCombatState(player);
        var top = state.DrawPile.Cards.Take(amount).ToList();
        if (top.Count == 0)
        {
            return;
        }

        int discarded = 0;
        foreach (var card in top)
        {
            double value;
            try
            {
                value = CardChoiceSupport.CardValue(card.Preview);
            }
            catch
            {
                value = 0;
            }

            if (value < 0)
            {
                context.Simulator.Discard(card);
                discarded++;
            }
        }

        // OnScry: Nirvana converts every scry into block equal to its stack count.
        int nirvana = context.State.CombatState is SimulatedCombatState combat
            ? combat.GetAmount<NirvanaPower>(player.Creature)
            : 0;
        if (nirvana > 0)
        {
            context.Simulator.GainBlock(
                player.Creature,
                nirvana,
                MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered);
        }

        // The scry decision itself is not part of the search, and two side effects cannot be reached from
        // here: FlurryOfBlows returning from the discard pile, and Prophecy triggers. Flag the branch so
        // the overlay reports a scry was approximated instead of pretending it was exact.
        context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);

        EngineDiagnostics.Warn(
            $"[WatcherAdapter] scry peeked {top.Count}, discarded {discarded} (decision fixed by adapter)");
    }
}
