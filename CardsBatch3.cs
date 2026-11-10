using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 3: cards whose OnPlay is a command sequence plus one piece of branch state the earlier batches
/// did not touch — hand size, damage actually dealt, an extra conditional block, or a generated card.
/// Each entry follows the mod's OnPlay body in order; nothing here guesses at a number the branch can
/// read exactly.
/// </summary>
internal static class CardsBatch3
{
    internal static void RegisterAll()
    {
        // Spirit Shield: block = hand size x MagicNumber, move-flagged.
        Register<WatcherMod.WatcherSpiritShield>("WatcherSpiritShield", (card, context) =>
        {
            var env = StanceEnv.From(context);
            int hand = context.State.GetPlayerCombatState(card.Owner).Hand.Cards.Count;
            int amount = hand * Ops.Var(card, context, "MagicNumber");
            if (amount > 0)
            {
                context.GainBlock(card.Owner.Creature, amount, ValueProp.Move);
            }
        });

        // Wallop: attack, then gain block equal to the HP the target actually lost.
        Register<WatcherMod.WatcherWallop>("WatcherWallop", (card, context) =>
        {
            int before = context.Target.CurrentHp;
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            int dealt = before - context.Target.CurrentHp;
            if (dealt > 0)
            {
                context.GainBlock(card.Owner.Creature, dealt, ValueProp.Unpowered | ValueProp.Move);
            }
        });

        // Halt: block from the Block var, plus MagicNumber block while in Wrath.
        Register<WatcherMod.WatcherHalt>("WatcherHalt", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            if (!StanceOps.IsInStance(env, card.Owner.Creature, StanceOps.Wrath))
            {
                return;
            }

            int extra = Ops.Var(card, context, "MagicNumber");
            if (extra > 0)
            {
                context.GainBlock(card.Owner.Creature, extra, ValueProp.Move);
            }
        });

        // Vigilance: block, then enter Calm.
        Register<WatcherMod.WatcherVigilance>("WatcherVigilance", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            StanceOps.Enter(card, context, StanceOps.Calm);
        });

        // Serenity: block, set RestfulPower to 25, then enter Calm.
        Register<WatcherMod.WatcherSerenity>("WatcherSerenity", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            env.Sink.ApplyPowerFromSource(typeof(WatcherMod.RestfulPower), card.Owner.Creature, 25, card.Owner.Creature, card);
            var restful = env.Combat.GetPower<WatcherMod.RestfulPower>(card.Owner.Creature);
            if (restful != null)
            {
                env.Sink.SetPowerAmount(restful, 25);
            }

            StanceOps.Enter(card, context, StanceOps.Calm);
        });

        // Inner Peace: in Calm draw MagicNumber, otherwise enter Calm.
        Register<WatcherMod.WatcherInnerPeace>("WatcherInnerPeace", (card, context) =>
        {
            var env = StanceEnv.From(context);
            if (StanceOps.IsInStance(env, card.Owner.Creature, StanceOps.Calm))
            {
                int draw = Ops.Var(card, context, "MagicNumber");
                if (draw > 0)
                {
                    env.Simulator.Draw(card.Owner, draw);
                }

                return;
            }

            StanceOps.Enter(card, context, StanceOps.Calm);
        });

        // Evaluate: block, then shuffle one Insight into the draw pile.
        Register<WatcherMod.WatcherEvaluate>("WatcherEvaluate", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherInsight>(
                card.Owner,
                PileType.Draw,
                1,
                card.Owner,
                CardPilePosition.Random);
        });

        // Prostrate: block, then gain MagicNumber Mantra (10 Mantra converts into Divinity).
        Register<WatcherMod.WatcherProstrate>("WatcherProstrate", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            MantraOps.Gain(env, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
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
