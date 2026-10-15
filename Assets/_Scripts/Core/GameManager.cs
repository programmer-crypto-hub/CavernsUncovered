using System;
using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu = 0,
        Playing = 1,
        Combat = 2,
        Lost = 3,
        Won = 4,
    }

    [SerializeField] private GameEventRegistry events;
    public static GameEventRegistry Events => Instance != null ? Instance.events : null;

    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public GameState CurrentState { get; private set; }

    private GameState _lastLocalState;
    private bool _isNetworkInitialized = false;
    // Local state fallback queue if a script requests a state change too early on boot
    private GameState? _pendingStateRequest = null;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _lastLocalState = GameState.MainMenu;
    }

    public override void Spawned()
    {
        _isNetworkInitialized = true;
        // Establish the initial runtime baseline state securely on the Host
        if (HasStateAuthority && CurrentState == GameState.MainMenu)
        {
            CurrentState = GameState.Playing;
        }

        Instance = this;

        // Match the initial local frame track cache
        _lastLocalState = CurrentState;

        if (_pendingStateRequest.HasValue && HasStateAuthority)
        {
            CurrentState = _pendingStateRequest.Value;
            _pendingStateRequest = null;
        }
        else if (HasStateAuthority && CurrentState == GameState.MainMenu)
        {
            CurrentState = GameState.Combat;
        }
    }

    public void RaiseMapGenerated()
    {
        Debug.Log("Event Fired: OnMapGenerated.");
        events.OnMapGenerated?.Invoke();
    }

    public void RequestStateChange(GameState newState)
    {
        // Safety Barrier: If Fusion isn't ready yet, queue the request safely in local C# memory
        if (!_isNetworkInitialized)
        {
            Debug.LogWarning($"[GAME MANAGER] Network not ready. Queuing state request to: {newState}");
            _pendingStateRequest = newState;
            return;
        }

        // If we are ready, route it cleanly down to the network authority layer
        RPC_RequestStateChange(newState);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestStateChange(GameState newState)
    {
        // FIXED: Enforce state authority and assign variable.
        // DO NOT call OnStateChanged manually here! Fusion's OnChangedRender attribute handles it automatically.
        if (HasStateAuthority)
        {
            CurrentState = newState;
            Debug.Log($"Authority verified. State updated to: {newState}");
        }
    }

    // Fired automatically by Photon Fusion 2 on EVERY client when CurrentState replicates!
    private void OnStateChanged()
    {
        GameState oldState = _lastLocalState;
        GameState newState = CurrentState;
        _lastLocalState = newState;

        Debug.LogWarning($"[STATE TRANSITION] Replicated switch from {oldState} ➡️ {newState}");

        switch (newState)
        {
            case GameState.MainMenu: HandleMainMenu(); break;
            case GameState.Playing: HandleExploration(); break;
            case GameState.Combat: HandleCombat(); break;
            case GameState.Lost:
            case GameState.Won: HandleGameOver(newState); break;
        }

        if (Events != null)
        {
            RaiseEvent(Events.OnGameStateTransition, oldState, newState);
        }
    }

    public void HandleExploration()
    {
        Debug.Log("Handling exploration mode.");
        Time.timeScale = 1f;

        // Allow local movement, unlock player input states natively
        PlayerInputHandler.IsMyTurn = true;

        ShopUIManager.Instance?.ToggleShop(false);
    }

    private void HandleCombat()
    {
        Time.timeScale = 1f;

        PlayerInputHandler.IsMyTurn = true;
        PlayerInputHandler.IsUIActive = true;
    }

    public void HandleGameOver(GameState state)
    {
        Time.timeScale = 0f;
        PlayerInputHandler.IsUIActive = true;
    }

    public void HandleMainMenu()
    {
        Time.timeScale = 1f;
        PlayerInputHandler.IsUIActive = true;
    }
    public void RaiseEvent(Action action) => action?.Invoke();
    public void RaiseEvent<T>(Action<T> action, T arg) => action?.Invoke(arg);
    public void RaiseEvent<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2) => action?.Invoke(arg1, arg2);
}
