using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 5: draw / energy cards, plus the two "previous card type" cards that pay off with draw or energy.
/// </summary>
internal static class CardsBatch5
{
    internal static void RegisterAll()
    {
        // Miracle: gain the Energy dynamic var.
        Register<WatcherMod.WatcherMiracle>("WatcherMiracle", (card, context) =>
        {
            int energy = Ops.Var(card, context, "Energy");
            if (energy > 0)
            {
                context.Simulator.GainEnergy(card.Owner, energy);
            }
        });

        // Insight: draw MagicNumber.
        Register<WatcherMod.WatcherInsight>("WatcherInsight", (card, context) =>
        {
            int draw = Ops.Var(card, context, "MagicNumber");
            if (draw > 0)
            {
                context.Simulator.Draw(card.Owner, draw);
            }
        });

        // Empty Mind: draw MagicNumber, then leave the stance.
        Register<WatcherMod.WatcherEmptyMind>("WatcherEmptyMind", (card, context) =>
        {
            int draw = Ops.Var(card, context, "MagicNumber");
            if (draw > 0)
            {
                context.Simulator.Draw(card.Owner, draw);
            }

            if (Ops.Pending(context))
            {
                return;
            }

            StanceOps.Exit(card, context);
        });

        // Wheel Kick: attack, then draw MagicNumber.
        Register<WatcherMod.WatcherWheelKick>("WatcherWheelKick", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            int draw = Ops.Var(card, context, "MagicNumber");
            if (draw > 0)
            {
                context.Simulator.Draw(card.Owner, draw);
            }
        });

        // Follow-Up: attack; if the previous card was an Attack, gain the Energy var.
        Register<WatcherMod.WatcherFollowUp>("WatcherFollowUp", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            if (PlayHistory.PreviousPlayedType(context.History) != CardType.Attack)
            {
                return;
            }

            int energy = Ops.Var(card, context, "Energy");
            if (energy > 0)
            {
                context.Simulator.GainEnergy(card.Owner, energy);
            }
        });

        // Sanctity: block; if the previous card was a Skill, draw MagicNumber.
        Register<WatcherMod.WatcherSanctity>("WatcherSanctity", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            if (PlayHistory.PreviousPlayedType(context.History) != CardType.Skill)
            {
                return;
            }

            int draw = Ops.Var(card, context, "MagicNumber");
            if (draw > 0)
            {
                context.Simulator.Draw(card.Owner, draw);
            }
        });

        // Deceive Reality: block, then create Safety into the hand.
        Register<WatcherMod.WatcherDeceiveReality>("WatcherDeceiveReality", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherSafety>(
                card.Owner,
                PileType.Hand,
                1,
                card.Owner);
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
