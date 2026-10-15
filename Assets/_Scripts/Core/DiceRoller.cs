using UnityEngine;
using Fusion;
using System;

public class DiceRoller : NetworkBehaviour
{
    public static DiceRoller Instance { get; private set; }

    // 1. Network the result. Render() will catch when it changes!
    [Networked] public int DiceRollResult { get; set; } = -1;

    public override void Spawned()
    {
        if (Instance == null) Instance = this;
    }

    public void RequestRollDice()
    {
        if (GetComponent<NetworkObject>().Runner.IsServer)
        {
            DiceRollResult = UnityEngine.Random.Range(1, 21);

            GameManager.Instance.RaiseEvent(GameManager.Events.OnDiceRolled, DiceRollResult);

            Debug.LogWarning($"[DICESUCCESS] Сервер выбросил на кубике: {DiceRollResult}");

            if (DiceUI.Instance != null) DiceUI.Instance.HandleDiceRolled(DiceRollResult);
        }
    }
    public int GetDiceResult()
    {
        return DiceRollResult;
    }
    public override void Render()
    {
        // 2. State Sync: Detect when the roll arrives
        if (DiceRollResult > 0)
        {
            GameManager.Instance.RaiseEvent(GameManager.Events.OnDiceRolled, DiceRollResult);
        }
    }

    // This logic translates score into grid spaces (No UI involved!)
    public int ConvertDiceToMovement()
    {
        int spaces = Mathf.RoundToInt(DiceRollResult / 3.0f);
        return spaces == 0 ? 1 : spaces; // Ensure at least 1 space
    }
    public float ConvertDiceToCombat()
    {
        int diceToCombatApprox = 100 / 5;
        float damage = ((float)DiceRollResult / diceToCombatApprox);
        Debug.Log($"Calculating damage, via formula {DiceRollResult} / {diceToCombatApprox}, which results in {damage}");
        return damage;
    }

    public void ResetDice()
    {
        if (HasStateAuthority) DiceRollResult = -1;
    }
}
