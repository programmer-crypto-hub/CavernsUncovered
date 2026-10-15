using Fusion;
using UnityEngine;
using UnityEngine.UI;

public enum ParticipantType : byte { Player = 0, Boss = 1 }

public struct Participant : INetworkStruct
{
    public int Id;           // Player: NetworkObject.Id.Raw. Boss: NetworkObject.Id.Raw + 1000.
    public PlayerRef Owner;  // The client allowed to act for this Id. PlayerRef.None for AI/boss.
    public ParticipantType Type;
}

public class GameSession : NetworkBehaviour
{
    public static GameSession Instance;

    [Networked, OnChangedRender(nameof(OnTurnChanged))]
    public int CurrentTurnID { get; set; } = -1;

    [Networked, Capacity(12)]
    public NetworkArray<Participant> Participants => default;

    [Networked] public int TotalParticipants { get; set; }

    private int _lastProcessedTurnID = -2;

    public override void Spawned()
    {
        Instance = this;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RegisterPlayer(int id, RpcInfo info = default)
    {
        if (!Runner.TryFindObject(new NetworkId { Raw = (uint)id }, out NetworkObject playerObj))
        {
            Debug.LogWarning($"RPC_RegisterPlayer: no NetworkObject found for id {id}.");
            return;
        }

        PlayerRef owner = playerObj.InputAuthority;
        if (owner == PlayerRef.None)
        {
            Debug.LogWarning($"RPC_RegisterPlayer: object {id} has no input authority.");
            return;
        }

        // A None source means the host called this locally, which is trusted.
        if (info.Source != PlayerRef.None && info.Source != owner)
        {
            Debug.LogWarning($"Rejected registration of {id} from {info.Source}: owner is {owner}.");
            return;
        }

        RegisterInternal(id, owner, ParticipantType.Player);
    }

    public void RegisterBoss(int id)
    {
        if (!HasStateAuthority) return;
        RegisterInternal(id, PlayerRef.None, ParticipantType.Boss);
    }

    private void RegisterInternal(int id, PlayerRef owner, ParticipantType type)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < TotalParticipants; i++)
        {
            if (Participants[i].Id == id) return; // already registered
        }

        if (TotalParticipants >= Participants.Length)
        {
            Debug.LogWarning("GameSession: Participant capacity reached, ignoring registration.");
            return;
        }

        Participants.Set(TotalParticipants, new Participant { Id = id, Owner = owner, Type = type });
        TotalParticipants++;

        if (CurrentTurnID == -1 && type == ParticipantType.Player)
        {
            CurrentTurnID = id;
        }
        Debug.Log($"Participant {id} ({type}) registered. Total: {TotalParticipants}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndTurn(RpcInfo info = default)
    {
        if (TotalParticipants == 0) return;

        int currentIndex = FindParticipantIndex(CurrentTurnID);
        if (currentIndex == -1) currentIndex = 0;

        Participant current = Participants[currentIndex];
        PlayerRef sender = info.Source == PlayerRef.None ? Runner.LocalPlayer : info.Source;
        if (current.Type == ParticipantType.Player && current.Owner != sender)
        {
            Debug.LogWarning($"Rejected end-turn request from {info.Source}: not their turn.");
            return;
        }

        if (current.Type == ParticipantType.Boss)
        {
            Debug.LogWarning("Rejected end-turn request: current turn belongs to a boss (AI-controlled).");
            return;
        }

        AdvanceTurn(currentIndex);
    }

    public void AdvanceFromBossTurn()
    {
        if (!HasStateAuthority) return;

        int currentIndex = FindParticipantIndex(CurrentTurnID);
        if (currentIndex == -1) return;
        if (Participants[currentIndex].Type != ParticipantType.Boss) return;

        AdvanceTurn(currentIndex);
    }

    private void AdvanceTurn(int currentIndex)
    {
        int nextIndex = (currentIndex + 1) % TotalParticipants;
        CurrentTurnID = Participants[nextIndex].Id;
        Debug.Log($"Turn switched to ID: {CurrentTurnID}");
    }

    private int FindParticipantIndex(int id)
    {
        for (int i = 0; i < TotalParticipants; i++)
        {
            if (Participants[i].Id == id) return i;
        }
        return -1;
    }
    public void OnTurnChanged()
    {
        if (CurrentTurnID == -1) return;
        if (CurrentTurnID == _lastProcessedTurnID) return;
        _lastProcessedTurnID = CurrentTurnID;

        int idx = FindParticipantIndex(CurrentTurnID);
        bool isLocalPlayersTurn = idx != -1
            && Participants[idx].Type == ParticipantType.Player
            && Participants[idx].Owner == Runner.LocalPlayer;
        Debug.Log($"Current player turn changed to ID: {CurrentTurnID}. Is local player's turn: {isLocalPlayersTurn}. Why? Isn't idx -1? {idx != -1}, " +
            $"Is the turn owned by the local player? {Participants[idx].Type == ParticipantType.Player}, " +
            $"Is the owner the local player? {Participants[idx].Owner == Runner.LocalPlayer}");
        PlayerInputHandler.IsMyTurn = isLocalPlayersTurn;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RaiseEvent(GameManager.Events.OnTurnChanged, CurrentTurnID);
        }
    }
}