using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerInputReader : MonoBehaviour
{
    [Header("Optional Input Action Asset")]
    [SerializeField] private InputActionAsset inputActionsAsset;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string dashActionName = "Sprint";
    [SerializeField] private string primaryAttackActionName = "Attack";
    [SerializeField] private string secondaryAttackActionName = "Interact";

    private InputActionMap actionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private InputAction primaryAttackAction;
    private InputAction secondaryAttackAction;
    private bool ownsActionMap;

    private bool jumpQueued;
    private bool dashQueued;
    private bool primaryAttackQueued;
    private bool secondaryAttackQueued;

    public Vector2 MoveInput { get; private set; }

    private void Awake()
    {
        ResolveActions();
    }

    private void OnEnable()
    {
        ResolveActions();
        actionMap?.Enable();
    }

    private void OnDisable()
    {
        actionMap?.Disable();
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();

        if (ownsActionMap && actionMap != null)
        {
            actionMap.Dispose();
            actionMap = null;
        }
    }

    public bool ConsumeJumpPressed()
    {
        return ConsumeFlag(ref jumpQueued);
    }

    public bool ConsumeDashPressed()
    {
        return ConsumeFlag(ref dashQueued);
    }

    public bool ConsumePrimaryAttackPressed()
    {
        return ConsumeFlag(ref primaryAttackQueued);
    }

    public bool ConsumeSecondaryAttackPressed()
    {
        return ConsumeFlag(ref secondaryAttackQueued);
    }

    private void ResolveActions()
    {
        if (moveAction != null)
        {
            return;
        }

        if (inputActionsAsset != null)
        {
            InputActionMap resolvedMap = inputActionsAsset.FindActionMap(actionMapName, false);
            if (TryAssignActions(resolvedMap))
            {
                return;
            }

            Debug.LogWarning($"[{nameof(PlayerInputReader)}] Could not find all requested actions in '{actionMapName}'. Falling back to runtime actions.", this);
        }

        actionMap = CreateFallbackActionMap();
        ownsActionMap = true;
        RegisterActions(actionMap);
    }

    private bool TryAssignActions(InputActionMap resolvedMap)
    {
        if (resolvedMap == null)
        {
            return false;
        }

        InputAction resolvedMove = resolvedMap.FindAction(moveActionName, false);
        InputAction resolvedJump = resolvedMap.FindAction(jumpActionName, false);
        InputAction resolvedDash = resolvedMap.FindAction(dashActionName, false);
        InputAction resolvedPrimaryAttack = resolvedMap.FindAction(primaryAttackActionName, false);
        InputAction resolvedSecondaryAttack = resolvedMap.FindAction(secondaryAttackActionName, false);

        if (resolvedMove == null ||
            resolvedJump == null ||
            resolvedDash == null ||
            resolvedPrimaryAttack == null ||
            resolvedSecondaryAttack == null)
        {
            return false;
        }

        actionMap = resolvedMap;
        RegisterActions(actionMap);
        return true;
    }

    private void RegisterActions(InputActionMap resolvedMap)
    {
        actionMap = resolvedMap;
        moveAction = actionMap.FindAction(moveActionName, true);
        jumpAction = actionMap.FindAction(jumpActionName, true);
        dashAction = actionMap.FindAction(dashActionName, true);
        primaryAttackAction = actionMap.FindAction(primaryAttackActionName, true);
        secondaryAttackAction = actionMap.FindAction(secondaryAttackActionName, true);

        UnregisterCallbacks();

        moveAction.performed += OnMovePerformed;
        moveAction.canceled += OnMoveCanceled;
        jumpAction.performed += OnJumpPerformed;
        dashAction.performed += OnDashPerformed;
        primaryAttackAction.performed += OnPrimaryAttackPerformed;
        secondaryAttackAction.performed += OnSecondaryAttackPerformed;
    }

    private void UnregisterCallbacks()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
        }

        if (dashAction != null)
        {
            dashAction.performed -= OnDashPerformed;
        }

        if (primaryAttackAction != null)
        {
            primaryAttackAction.performed -= OnPrimaryAttackPerformed;
        }

        if (secondaryAttackAction != null)
        {
            secondaryAttackAction.performed -= OnSecondaryAttackPerformed;
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        MoveInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpQueued = true;
    }

    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        dashQueued = true;
    }

    private void OnPrimaryAttackPerformed(InputAction.CallbackContext context)
    {
        primaryAttackQueued = true;
    }

    private void OnSecondaryAttackPerformed(InputAction.CallbackContext context)
    {
        secondaryAttackQueued = true;
    }

    private static bool ConsumeFlag(ref bool value)
    {
        if (!value)
        {
            return false;
        }

        value = false;
        return true;
    }

    private InputActionMap CreateFallbackActionMap()
    {
        InputActionMap map = new InputActionMap(actionMapName);

        InputAction move = map.AddAction(moveActionName, InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        move.AddBinding("<Gamepad>/leftStick");
        move.AddBinding("<Joystick>/stick");

        InputAction jump = map.AddAction(jumpActionName, InputActionType.Button);
        jump.AddBinding("<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");

        InputAction dash = map.AddAction(dashActionName, InputActionType.Button);
        dash.AddBinding("<Keyboard>/leftShift");
        dash.AddBinding("<Gamepad>/leftStickPress");

        InputAction primaryAttack = map.AddAction(primaryAttackActionName, InputActionType.Button);
        primaryAttack.AddBinding("<Mouse>/leftButton");
        primaryAttack.AddBinding("<Gamepad>/buttonWest");

        InputAction secondaryAttack = map.AddAction(secondaryAttackActionName, InputActionType.Button);
        secondaryAttack.AddBinding("<Keyboard>/e");
        secondaryAttack.AddBinding("<Gamepad>/buttonNorth");

        return map;
    }
}
