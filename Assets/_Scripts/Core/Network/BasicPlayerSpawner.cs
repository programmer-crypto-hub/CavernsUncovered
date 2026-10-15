using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class BasicPlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkObject _playerPrefab;
    [SerializeField] private GameObject gameManagerNetworkPrefab;

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    private bool _gameManagerSpawned = false;
    // Kicked off by a UI Button
    public void StartHostFromButton()
    {
        if (_runner != null) return;
        StartGame(GameMode.AutoHostOrClient);
    }

    private async void StartGame(GameMode mode)
    {
        // 1. Setup the Network Runner GameObject cleanly
        GameObject go = new GameObject("Fusion_Network_Runner");
        _runner = go.AddComponent<NetworkRunner>();
        go.AddComponent<RunnerSimulatePhysics3D>();
        DontDestroyOnLoad(go);
        DontDestroyOnLoad(this.gameObject);


        // 2. Register callbacks ONCE right here
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        // 3. Setup standard scene management
        var sceneManager = go.AddComponent<NetworkSceneManagerDefault>();

        try
        {
            int targetSceneIndex = 3; // Your GameScene Index
            await _runner.StartGame(new StartGameArgs()
            {
                GameMode = mode,
                SessionName = "GameRoom",
                Scene = SceneRef.FromIndex(targetSceneIndex),
                SceneManager = sceneManager
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Fatal Error during StartGame: {e.Message}");
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var localPlayerObj = runner.GetPlayerObject(runner.LocalPlayer);
        if (localPlayerObj == null) return;

        var inputHandler = localPlayerObj.GetComponent<PlayerInputHandler>();
        if (inputHandler != null)
        {
            inputHandler.PopulatedNetworkInput(out var data);

            input.Set(data);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene();
        }
        if (!runner.IsServer || _spawnedCharacters.ContainsKey(player)) return;

        Vector3 spawnPos = new Vector3((player.RawEncoded % 5) * 2f, 0f, 0f);
        Quaternion spawnRotation = Quaternion.Euler(-90f, 0f, 0f);
        var networkPlayer = runner.Spawn(_playerPrefab, spawnPos, spawnRotation, player);

        if (networkPlayer != null)
        {
            _spawnedCharacters.Add(player, networkPlayer);
            runner.SetPlayerObject(player, networkPlayer);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.HandleExploration();
                GameManager.Instance.RequestStateChange(GameManager.GameState.Playing);
            }
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;
        if (_gameManagerSpawned) return;
        runner.Spawn(gameManagerNetworkPrefab, Vector3.zero, Quaternion.identity);
        _gameManagerSpawned = true;
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            if (networkObject != null) runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    // Unregister callbacks if this manager component is destroyed to prevent leaks
    private void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.RemoveCallbacks(this);
        }
    }

    // Boilerplates required by the INetworkRunnerCallbacks Interface
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
