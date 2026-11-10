using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using CombatSolver.Engine.InCombat.Mirrors.Cards;

namespace WatcherAdapter;

/// <summary>
/// Mirrors for the Watcher cards whose <c>IsPlayable</c> is conditional.
///
/// The solver answers "always playable" for any card it has no mirror for, and the real getter reads the
/// live hand — so an unmirrored conditional card looks free to play and the search builds routes around
/// a move the player cannot actually make. Only cards with a real condition belong here.
/// </summary>
internal static class Playability
{
    internal static void RegisterAll()
    {
        Register<WatcherMod.WatcherSignatureMove>("WatcherSignatureMove", (card, context) =>
        {
            // SignatureMove: playable only while at most one attack is in hand
            // (WatcherSignatureMove.IsPlayable).
            var hand = context.State.GetPlayerCombatState(card.Owner).Hand.Cards;
            int attacks = hand.Count(handCard => handCard.Preview.Type == CardType.Attack);
            return attacks <= 1;
        });
    }

    private static void Register<T>(string name, System.Func<T, CardIsPlayableMirrorContext, bool> handler)
        where T : MegaCrit.Sts2.Core.Models.CardModel
    {
        try
        {
            CardIsPlayableMirrors.Registry.Register(handler);
            Entry.Log($"playability mirror registered: {name}");
        }
        catch (System.Exception ex)
        {
            Entry.Log($"FAILED to register playability for {name}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
