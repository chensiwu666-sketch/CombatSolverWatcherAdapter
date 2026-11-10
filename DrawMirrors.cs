using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;

namespace WatcherAdapter;

/// <summary>
/// Mirrors for cards whose effect happens when they are drawn rather than played.
///
/// Deus Ex Machina is unplayable and exhausts itself the moment it reaches the hand, then creates
/// MagicNumber Miracles. The mod does this from AfterCardDrawn; the solver dispatches the same hook, so
/// the branch reproduces it: exhaust the drawn card, then generate the Miracles into the hand.
/// </summary>
internal static class DrawMirrors
{
    internal static void RegisterAll()
    {
        try
        {
            AfterCardDrawnMirrors.Registry.Register<WatcherMod.WatcherDeusExMachina>((card, context) =>
            {
                if (!ReferenceEquals(context.InitialCard, card))
                {
                    return;
                }

                var env = StanceEnv.FromParts(context.CombatState, context.Simulator, context.History);
                var state = context.State.GetPlayerCombatState(card.Owner);
                var drawn = state.Hand.Cards.FirstOrDefault(c => ReferenceEquals(c.Preview, card));
                if (drawn == null)
                {
                    return;
                }

                int miracles = 0;
                if (card.DynamicVars.TryGetValue("MagicNumber", out var var))
                {
                    miracles = (int)var.BaseValue;
                }

                context.Simulator.Exhaust(drawn);
                if (miracles > 0)
                {
                    env.Simulator.CreateAndAddGeneratedCardsToCombat<WatcherMod.WatcherMiracle>(
                        card.Owner,
                        PileType.Hand,
                        miracles,
                        card.Owner);
                }
            });
            Entry.Log("draw mirror registered: WatcherDeusExMachina (exhaust on draw + create Miracles)");
        }
        catch (Exception ex)
        {
            Entry.Log($"FAILED to register WatcherDeusExMachina draw mirror: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
