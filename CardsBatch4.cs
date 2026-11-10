using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using CombatSolver.Engine.InCombat.Mirrors.Cards.OnPlay;

namespace WatcherAdapter;

/// <summary>
/// Batch 4: cards whose extra effect depends on the type of the card played just before them.
///
/// The branch history records every finished play, so this is read, not guessed: take the last finished
/// play and use its card type the same way the mod reads the live pile.
/// </summary>
internal static class CardsBatch4
{
    internal static void RegisterAll()
    {
        // Crush Joints: attack; if the previous card was a Skill, apply Vulnerable.
        Register<WatcherMod.WatcherCrushJoints>("WatcherCrushJoints", (card, context) =>
        {
            Ops.Attack(card, context);
            if (Ops.Pending(context))
            {
                return;
            }

            if (PlayHistory.PreviousPlayedType(context.History) != CardType.Skill)
            {
                return;
            }

            Ops.Power(card, context, typeof(VulnerablePower), context.Target, Ops.Var(card, context, "Vulnerable"));
        });

        // Sash Whip: attack; if the previous card was an Attack, apply Weak.
        Register<WatcherMod.WatcherSashWhip>("WatcherSashWhip", (card, context) =>
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

            Ops.Power(card, context, typeof(WeakPower), context.Target, Ops.Var(card, context, "MagicNumber"));
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
