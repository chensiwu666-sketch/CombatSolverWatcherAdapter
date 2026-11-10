using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 8: the scry cards (approximated, see <see cref="ScryOps"/>) and the two plain draw cards that
/// were blocked only by a missing batch.
/// </summary>
internal static class CardsBatch8
{
    internal static void RegisterAll()
    {
        // Third Eye: block, then scry MagicNumber.
        Register<WatcherMod.WatcherThirdEye>("WatcherThirdEye", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            ScryOps.Do(context, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
        });

        // Cut Through Fate: attack, scry MagicNumber, then draw one.
        Register<WatcherMod.WatcherCutThroughFate>("WatcherCutThroughFate", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            ScryOps.Do(context, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
            if (Ops.Pending(context))
            {
                return;
            }

            context.Simulator.Draw(card.Owner, 1);
        });

        // Just Lucky: scry, block, then attack.
        Register<WatcherMod.WatcherJustLucky>("WatcherJustLucky", (card, context) =>
        {
            ScryOps.Do(context, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
            if (Ops.Pending(context))
            {
                return;
            }

            Ops.Block(card, context);
            Ops.Attack(card, context);
        });

        // Scrawl: draw up to ten cards in hand.
        Register<WatcherMod.WatcherScrawl_P>("WatcherScrawl_P", (card, context) =>
        {
            int hand = context.State.GetPlayerCombatState(card.Owner).Hand.Cards.Count;
            int draw = 10 - hand;
            if (draw > 0)
            {
                context.Simulator.Draw(card.Owner, draw);
            }
        });
    }

    private static void Register<T>(string name, Action<T, CardOnPlayMirrorContext> handler)
        where T : CardModel
    {
        try
        {
            CardOnPlayMirrors.Registry.Register(handler);
            Entry.Log($"mirror registered: {name}");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
