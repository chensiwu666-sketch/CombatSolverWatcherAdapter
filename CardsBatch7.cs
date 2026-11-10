using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 7: conditional stance cards, mantra/generation cards, mark damage and the kill-threshold card.
/// </summary>
internal static class CardsBatch7
{
    internal static void RegisterAll()
    {
        // Indignation: in Wrath apply Vulnerable to every hittable enemy, otherwise enter Wrath.
        Register<WatcherMod.WatcherIndignation>("WatcherIndignation", (card, context) =>
        {
            var env = StanceEnv.From(context);
            if (StanceOps.IsInStance(env, card.Owner.Creature, StanceOps.Wrath))
            {
                int amount = Ops.Var(card, context, "MagicNumber");
                foreach (var enemy in env.Combat.HittableEnemies)
                {
                    Ops.Power(card, context, typeof(VulnerablePower), enemy, amount);
                }

                return;
            }

            StanceOps.Enter(card, context, StanceOps.Wrath);
        });

        // Pray: gain MagicNumber Mantra and shuffle one Insight into the draw pile.
        Register<WatcherMod.WatcherPray>("WatcherPray", (card, context) =>
        {
            var env = StanceEnv.From(context);
            MantraOps.Gain(env, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
            env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherInsight>(
                card.Owner,
                PileType.Draw,
                1,
                card.Owner,
                CardPilePosition.Random);
        });

        // Reach Heaven: attack, then shuffle Through Violence into the draw pile.
        Register<WatcherMod.WatcherReachHeaven>("WatcherReachHeaven", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherThroughViolence>(
                card.Owner,
                PileType.Draw,
                1,
                card.Owner,
                CardPilePosition.Random);
        });

        // Blasphemy: enter Divinity, then take End Turn Death Power.
        Register<WatcherMod.WatcherBlasphemy>("WatcherBlasphemy", (card, context) =>
        {
            StanceOps.Enter(card, context, StanceOps.Divinity);
            if (Ops.Pending(context))
            {
                return;
            }

            Ops.PowerSelf(card, context, typeof(WatcherMod.EndTurnDeathPower), 1);
            // EndTurnDeathPower's lethal hook is third-party code the search never runs, so the branch
            // is flagged: the player would die at end of turn in the real game.
            context.History.RecordRisk(CombatSolver.Engine.Common.PredictionRiskReason.MethodMirrorIncomplete);
        });

        // Sanctification: other players enter Divinity, the caster takes End Turn Death Power.
        Register<WatcherMod.WatcherSanctification>("WatcherSanctification", (card, context) =>
        {
            // Single player: the teammate loop in the mod's OnPlay is empty, so only the death power
            // applies. Flag it because that power's end-of-turn death is not modelled.
            Ops.PowerSelf(card, context, typeof(WatcherMod.EndTurnDeathPower), 1);
            context.History.RecordRisk(CombatSolver.Engine.Common.PredictionRiskReason.MethodMirrorIncomplete);
        });

        // Pressure Points: mark the target, then every marked enemy takes damage equal to its marks.
        Register<WatcherMod.WatcherPressurePoints>("WatcherPressurePoints", (card, context) =>
        {
            Ops.Power(card, context, typeof(WatcherMod.MarkPower), context.Target, Ops.Var(card, context, "MagicNumber"));
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            foreach (var enemy in env.Combat.HittableEnemies)
            {
                int marks = env.Combat.GetAmount<WatcherMod.MarkPower>(enemy);
                if (marks > 0)
                {
                    env.Simulator.Damage(enemy, marks, ValueProp.Unblockable | ValueProp.Unpowered, card.Owner.Creature);
                }
            }
        });

        // Swivel: block, then gain one Free Attack.
        Register<WatcherMod.WatcherSwivel>("WatcherSwivel", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            Ops.PowerSelf(card, context, typeof(FreeAttackPower), 1);
        });

        // Judgment: kill the target outright when its HP is at or below MagicNumber.
        Register<WatcherMod.WatcherJudgment>("WatcherJudgment", (card, context) =>
        {
            int threshold = Ops.Var(card, context, "MagicNumber");
            int hp = context.Target.CurrentHp;
            if (hp > 0 && hp <= threshold)
            {
                context.Simulator.Kill(context.Target);
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
