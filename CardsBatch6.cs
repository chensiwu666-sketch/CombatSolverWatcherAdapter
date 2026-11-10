using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 6: stance-exit attacks/blocks, mantra and power cards, and two generated-card plays.
/// All of these are plain command sequences; nothing here needs a mechanic the branch cannot read.
/// </summary>
internal static class CardsBatch6
{
    internal static void RegisterAll()
    {
        // Empty Fist: attack, then leave the stance.
        Register<WatcherMod.WatcherEmptyFist>("WatcherEmptyFist", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            StanceOps.Exit(card, context);
        });

        // Empty Body: block, then leave the stance.
        Register<WatcherMod.WatcherEmptyBody>("WatcherEmptyBody", (card, context) =>
        {
            Ops.Block(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            StanceOps.Exit(card, context);
        });

        // Worship: gain MagicNumber Mantra (converting every full 10 into Divinity).
        Register<WatcherMod.WatcherWorship>("WatcherWorship", (card, context) =>
        {
            var env = StanceEnv.From(context);
            MantraOps.Gain(env, card.Owner, Ops.Var(card, context, "MagicNumber"), card);
        });

        // Battle Hymn: apply BattleHymnPower with MagicNumber.
        Register<WatcherMod.WatcherBattleHymn>("WatcherBattleHymn", (card, context) =>
            Ops.PowerSelf(card, context, typeof(WatcherMod.BattleHymnPower), Ops.Var(card, context, "MagicNumber")));

        // Devotion (the card): apply DevotionPower with MagicNumber.
        Register<WatcherMod.WatcherDevotion>("WatcherDevotion", (card, context) =>
            Ops.PowerSelf(card, context, typeof(WatcherMod.DevotionPower), Ops.Var(card, context, "MagicNumber")));

        // Ragnarok: MagicNumber hits spread over random opponents.
        Register<WatcherMod.WatcherRagnarok>("WatcherRagnarok", (card, context) =>
            Ops.AttackRandom(context, Ops.Var(card, context, "MagicNumber")));

        // Carve Reality: attack, then create Smite into the hand.
        Register<WatcherMod.WatcherCarveReality>("WatcherCarveReality", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            var env = StanceEnv.From(context);
            env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherSmite>(
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
