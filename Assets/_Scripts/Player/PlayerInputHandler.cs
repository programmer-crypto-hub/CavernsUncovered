using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;

public class PlayerInputHandler : MonoBehaviour
{
    // Local flags to track button presses for the current frame
    private bool _attackTriggered;
    private bool _interactPressed;
    private bool _diceRollTriggered;
    private bool _endTurnTriggered;
    private bool _toggleInventoryTriggered;
    private bool _toggleShopTriggered;
    private bool _mouseLeftTriggered;
    private bool _mouseRightTriggered;

    public static bool IsUIActive { get; set; } = false;
    public static bool IsMyTurn { get; set; } = false;

    private void Update()
    {
        // Read the keyboard and mouse states without using the new Input System
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        Debug.Log($"Keyboard: {keyboard}, Mouse: {mouse}, IsMyTurn: {IsMyTurn}");
        if (keyboard == null || !IsMyTurn) return;

        // Fixate key presses (analogous to GetKeyDown)
        if (keyboard.aKey.wasPressedThisFrame) _attackTriggered = true;
        if (keyboard.eKey.wasPressedThisFrame) _interactPressed = true;
        if (keyboard.rKey.wasPressedThisFrame) _diceRollTriggered = true;
        if (keyboard.enterKey.wasPressedThisFrame) _endTurnTriggered = true;
        if (keyboard.iKey.wasPressedThisFrame) _toggleInventoryTriggered = true;
        if (keyboard.sKey.wasPressedThisFrame) _toggleShopTriggered = true;

        // Fixate mouse button presses (analogous to GetMouseButtonDown)
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) _mouseLeftTriggered = true;
            if (mouse.rightButton.wasPressedThisFrame) _mouseRightTriggered = true;
        }
    }

    public void PopulatedNetworkInput(out NetworkInputData data)
    {
        // Send the local flags to the network input data
        data.attackPressed = _attackTriggered;
        data.interactPressed = _interactPressed;
        data.diceRollPressed = _diceRollTriggered;
        data.endTurnPressed = _endTurnTriggered;
        data.toggleInventoryPressed = _toggleInventoryTriggered;
        data.toggleShopPressed = _toggleShopTriggered;
        data.mouseLeftClick = _mouseLeftTriggered;
        data.mouseRightClick = _mouseRightTriggered;

        // Reset local flags after sending to network
        _attackTriggered = false;
        _interactPressed = false;
        _diceRollTriggered = false;
        _endTurnTriggered = false;
        _toggleInventoryTriggered = false;
        _toggleShopTriggered = false;
        _mouseLeftTriggered = false;
        _mouseRightTriggered = false;  
    }
}
