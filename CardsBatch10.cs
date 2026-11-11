using System;
using System.Collections.Generic;
using System.Linq;
using CombatSolver;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;
using CombatSolver.Engine.InCombat.Simulation;

namespace WatcherAdapter;

/// <summary>
/// Batch 10: the cards whose effect starts with a player choice (冥想 / 通晓万物 / 许愿).
///
/// A third-party card's choice is invisible to the solver on its own: <c>CardChoiceSupport.GetSpec</c>
/// is a switch over vanilla card types, and its default branch means "this card has no choice". The
/// solver exposes <c>CardChoiceMirrors</c> for exactly this case — registering here makes the choice
/// expand into real search branches, and the chosen result is recorded in the plan so deployment can
/// answer the native page.
///
/// Ordering note: the solver runs the card mirror first, then expands the choice (Start → OnPlay →
/// OwnChoice → tail). 冥想's stance change and turn-end request therefore happen before the cards are
/// moved back to hand; the only difference from the mod is the order of two side effects that do not
/// feed each other.
/// </summary>
internal static class CardsBatch10
{
    internal static void RegisterAll()
    {
        RegisterMeditate();
        RegisterOmniscience();
        RegisterWish();
    }

    // 冥想: choose MagicNumber cards from the discard pile and return them to hand, enter Calm, then
    // end the turn when this is the last card of the series.
    private static void RegisterMeditate()
    {
        try
        {
            CardChoiceMirrors.Register<WatcherMod.WatcherMeditate>(
                (simulator, playedCard, card) =>
                {
                    var discard = simulator.State.GetPlayerCombatState(card.Owner).DiscardPile.Cards;
                    int count = Math.Min(ReadInt(card, "MagicNumber"), discard.Count);
                    if (count <= 0)
                    {
                        // The mod returns early and never opens a selector.
                        return null!;
                    }

                    var options = discard.ToList();
                    // The mod's TakeFromDiscard is non-cancelable, so the lower bound equals the upper.
                    return new CardChoiceSpec(
                        PlanChoiceEffect.MoveToHand,
                        PileType.Discard,
                        count,
                        count,
                        options,
                        options,
                        0d);
                },
                (simulator, combat, playedCard, card, choice) => true);
            Entry.Log("choice registered: WatcherMeditate (discard -> hand, exact count)");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register WatcherMeditate choice: {ex.GetType().Name}: {ex.Message}");
        }

        Register<WatcherMod.WatcherMeditate>("WatcherMeditate", (card, context) =>
        {
            // "GiveSingleTurnRetain" on each returned card is not modelled: the search tracks this
            // turn's hand, not next turn's retained cards. Flagged rather than silently dropped.
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);

            StanceOps.Enter(card, context, StanceOps.Calm);
            if (Ops.Pending(context))
            {
                return;
            }

            if (context.CardPlay.IsLastInSeries)
            {
                CardsBatch9.Combat(context).RequestPlayerTurnEnd();
            }
        });
    }

    // 通晓万物: choose one card from the draw pile and auto-play it twice, then exhaust it.
    private static void RegisterOmniscience()
    {
        try
        {
            CardChoiceMirrors.Register<WatcherMod.WatcherOmniscience>(
                (simulator, playedCard, card) =>
                {
                    var draw = simulator.State.GetPlayerCombatState(card.Owner).DrawPile.Cards;
                    if (draw.Count == 0)
                    {
                        return null!;
                    }

                    var options = draw.ToList();
                    return new CardChoiceSpec(
                        PlanChoiceEffect.ModDefined,
                        PileType.Draw,
                        1,
                        1,
                        options,
                        options,
                        0d);
                },
                (simulator, combat, playedCard, card, choice) =>
                {
                    if (choice.Cards.Count == 0)
                    {
                        return true;
                    }

                    var draw = simulator.State.GetPlayerCombatState(card.Owner).DrawPile.Cards;
                    var chosen = draw
                        .Where(candidate => candidate.Preview.Id.Entry == choice.Cards[0].CardId
                            && (choice.Cards[0].UpgradeLevel > 0) == candidate.Preview.IsUpgraded)
                        .Skip(choice.Cards[0].OptionOccurrence)
                        .FirstOrDefault();
                    if (chosen == null)
                    {
                        return false;
                    }

                    // The mod plays the card, then OmniscienceDoublePower makes AfterCardPlayed play it
                    // again. Playing it twice here is the same arithmetic without the extra power.
                    var deaths = new HashSet<uint>();
                    for (int play = 0; play < 2; play++)
                    {
                        if (chosen.GetPile(simulator.State) == null)
                        {
                            break;
                        }

                        if (!CardExecutionSupport.AutoPlay(
                                simulator,
                                combat,
                                chosen,
                                null,
                                deaths,
                                nestedChoiceSourceId: card.Id.Entry))
                        {
                            return false;
                        }
                    }

                    if (chosen.GetPile(simulator.State) is { } pile && pile.Type != PileType.Exhaust)
                    {
                        simulator.Exhaust(chosen);
                    }

                    return true;
                });
            Entry.Log("choice registered: WatcherOmniscience (draw pile -> auto-play twice)");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register WatcherOmniscience choice: {ex.GetType().Name}: {ex.Message}");
        }

        // The choice carries the whole effect; the mirror only marks the play so the solver does not
        // also apply a plain OnPlay.
        Register<WatcherMod.WatcherOmniscience>("WatcherOmniscience", (card, context) => { });
    }

    // 许愿: one of three fixed wishes — +Strength, Plated Armor, or gold. The options never exist in a
    // pile, so the option cards are only tokens for the plan; the payload is applied here by CardId.
    private static void RegisterWish()
    {
        try
        {
            CardChoiceMirrors.Register<WatcherMod.WatcherWish_P>(
                (simulator, playedCard, card) =>
                {
                    var owner = card.Owner;
                    var might = PredictedCard.Create(
                        CanonicalModels.Card<WatcherMod.WatcherWishAlmighty>(), owner);
                    var guard = PredictedCard.Create(
                        CanonicalModels.Card<WatcherMod.WatcherWishLiveForever>(), owner);
                    var wealth = PredictedCard.Create(
                        CanonicalModels.Card<WatcherMod.WatcherWishFameAndFortune>(), owner);
                    if (card.IsUpgraded)
                    {
                        might.Upgrade();
                        guard.Upgrade();
                        wealth.Upgrade();
                    }

                    var options = new List<PredictedCard> { might, guard, wealth };
                    return new CardChoiceSpec(
                        PlanChoiceEffect.ModDefined,
                        PileType.None,
                        1,
                        1,
                        options,
                        options,
                        0d);
                },
                (simulator, combat, playedCard, card, choice) =>
                {
                    if (choice.Cards.Count == 0)
                    {
                        return true;
                    }

                    var token = choice.Cards[0];
                    bool boosted = card.IsUpgraded || token.UpgradeLevel > 0;
                    var sink = (ICombatPredictionEffectSink)combat;
                    switch (token.CardId)
                    {
                        case "WATCHER_WISH_ALMIGHTY":
                            sink.ApplyPowerFromSource(
                                typeof(StrengthPower),
                                card.Owner.Creature,
                                boosted ? 4 : 3,
                                card.Owner.Creature,
                                card);
                            return true;
                        case "WATCHER_WISH_LIVE_FOREVER":
                            sink.ApplyPowerFromSource(
                                typeof(WatcherMod.WishPlatedArmorPower),
                                card.Owner.Creature,
                                boosted ? 8 : 6,
                                card.Owner.Creature,
                                card);
                            return true;
                        case "WATCHER_WISH_FAME_AND_FORTUNE":
                            int gained = combat.GainPlayerGold(simulator, card.Owner, boosted ? 30 : 25);
                            if (gained > 0)
                            {
                                combat.RecordLongTermResource(gained);
                            }

                            return true;
                        default:
                            return false;
                    }
                });
            Entry.Log("choice registered: WatcherWish_P (three fixed wishes)");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register WatcherWish_P choice: {ex.GetType().Name}: {ex.Message}");
        }

        Register<WatcherMod.WatcherWish_P>("WatcherWish_P", (card, context) => { });
    }

    private static int ReadInt(CardModel card, string key) =>
        card.DynamicVars.TryGetValue(key, out var dynamicVar) ? dynamicVar.IntValue : 0;

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
