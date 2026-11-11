using System;
using CombatSolver;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;

namespace WatcherAdapter;

/// <summary>
/// Batch 9: the cards that were listed as "cannot be done" but whose entry point already exists in
/// CombatSolver 0.50.1 — the generation chain (阿尔法/贝塔/聚能成刃), the end-turn cards (结末/腾跃),
/// the intent read (不惧妖邪) and the out-of-combat payload (勤学精进).
///
/// Every mirror here follows the mod's own OnPlay body line by line. Nothing is approximated; where
/// the solver genuinely cannot see a payload the branch carries a risk flag instead of a guess.
/// </summary>
internal static class CardsBatch9
{
    internal static void RegisterAll()
    {
        // 阿尔法: shuffle a 贝塔 into the draw pile at a random position.
        Register<WatcherMod.WatcherAlpha>("WatcherAlpha", (card, context) =>
            context.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherBeta>(
                card.Owner, PileType.Draw, 1, card.Owner, CardPilePosition.Random));

        // 贝塔: the same chain one step further — creates 欧米伽.
        Register<WatcherMod.WatcherBeta>("WatcherBeta", (card, context) =>
            context.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherOmega>(
                card.Owner, PileType.Draw, 1, card.Owner, CardPilePosition.Random));

        // 结末: damage every enemy, then end the turn when this is the last card of the series.
        // The mod guards with cardPlay.IsLastInSeries, which is the same flag the branch carries.
        Register<WatcherMod.WatcherConclude>("WatcherConclude", (card, context) =>
        {
            Ops.AttackAll(context, 1);
            if (Ops.Pending(context))
            {
                return;
            }

            if (context.CardPlay.IsLastInSeries)
            {
                Combat(context).RequestPlayerTurnEnd();
            }
        });

        // 不惧妖邪: attack the target; enter Calm when that target was attacking. The mod reads
        // WatcherSimpleAHelper.IsTargetAttacking, which is the live monster AI; the branch exposes the
        // same answer through SimulatedCombatState.IsEnemyIntendingToAttack.
        Register<WatcherMod.WatcherFearNoEvil>("WatcherFearNoEvil", (card, context) =>
        {
            var target = context.CardPlay.Target;
            bool targetWasAttacking = target != null && Combat(context).IsEnemyIntendingToAttack(target);

            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            if (targetWasAttacking)
            {
                StanceOps.Enter(card, context, StanceOps.Calm);
            }
        });

        // 勤学精进: the damage is exact. The fatal-kill payload upgrades a card in the deck, which is
        // out-of-combat state the search cannot model — flagged, never approximated.
        Register<WatcherMod.WatcherLessonLearned>("WatcherLessonLearned", (card, context) =>
        {
            Ops.Attack(card, context);
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
        });

        // 聚能成刃: X cost; create 灭除之刃 in the draw pile with Repeat = X, plus one when upgraded.
        // The generated card's Repeat *is* the mod's HitCount (WatcherExpunger.HitCount is a view over
        // DynamicVars.Repeat), so writing it here gives the attack the right hit count.
        Register<WatcherMod.WatcherConjureBlade>("WatcherConjureBlade", (card, context) =>
        {
            int x = context.Card.ResolveEnergyXValue(context.State);
            if (card.IsUpgraded)
            {
                x++;
            }

            if (x <= 0)
            {
                return;
            }

            var expunger = PredictedCard.Create(
                CanonicalModels.Card<WatcherMod.WatcherExpunger>(),
                card.Owner);
            expunger.MutablePreview.DynamicVars.Repeat.BaseValue = x;

            context.Simulator.AddGeneratedCardsToCombat(
                [expunger],
                PileType.Draw,
                card.Owner,
                CardPilePosition.Random,
                CardGenerationResultKind.Fixed);
        });

        // 腾跃: apply the mod's extra-turn marker power and end this turn. The power's
        // ShouldTakeExtraTurn / AfterTakingExtraTurn hooks are mirrored in HookMirrors.
        Register<WatcherMod.WatcherVault>("WatcherVault", (card, context) =>
        {
            Ops.PowerSelf(card, context, typeof(WatcherMod.WatcherExtraTurnPower), 1);
            Combat(context).RequestPlayerTurnEnd();
        });

        // 光辉: damage is the face value plus every Mantra gained this combat. The mod builds the attack
        // from `Damage.BaseValue + GetTotalMantraGained(owner)` and then runs the normal damage pipeline,
        // so the same thing here is to lift the branch card's base damage and let Ops.Attack apply
        // strength / stance multipliers on top. The base value is restored afterwards: the branch card
        // can be re-read by later steps and must not carry a phantom bonus.
        Register<WatcherMod.WatcherBrilliance>("WatcherBrilliance", (card, context) =>
        {
            int mantra = Combat(context)
                .GetPower<WatcherMod.WatcherStatePower>(card.Owner.Creature)?.TotalMantraGainedThisCombat ?? 0;

            if (mantra <= 0 || !card.DynamicVars.TryGetValue("Damage", out var damage))
            {
                Ops.Attack(card, context);
                return;
            }

            decimal original = damage.BaseValue;
            damage.BaseValue = original + mantra;
            try
            {
                Ops.Attack(card, context);
            }
            finally
            {
                damage.BaseValue = original;
            }
        });
    }

    internal static SimulatedCombatState Combat(CardOnPlayMirrorContext context) =>
        context.CombatState as SimulatedCombatState
        ?? throw new InvalidOperationException("card mirror requires a branch combat state");

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
