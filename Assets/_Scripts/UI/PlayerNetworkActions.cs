using Fusion;

// Executes a player's turn actions from network input.
// Replaces PlayerButtonController now that UI buttons are gone.
public class PlayerNetworkActions : NetworkBehaviour
{
    public override void FixedUpdateNetwork()
    {
        if (DiceRoller.Instance == null || GameSession.Instance == null) return;

        if (GetInput(out NetworkInputData data))
        {
            if (data.diceRollPressed)
            {
                DiceRoller.Instance.RequestRollDice();
            }
            if (data.endTurnPressed)
            {
                GameSession.Instance.RPC_RequestEndTurn();
            }
            // attackPressed, interactPressed, toggleInventoryPressed, toggleShopPressed,
            // mouseLeftClick, mouseRightClick — wire these in once those systems exist.
        }
    }
}