using System;
using UnityEngine;

public enum PlayerMotionState
{
    Idle,
    Move,
    Dash,
    Knockback
}

[DisallowMultipleComponent]
public class PlayerStateMachine : MonoBehaviour
{
    public PlayerMode CurrentMode { get; private set; } = PlayerMode.Water;
    public PlayerMotionState CurrentState { get; private set; } = PlayerMotionState.Idle;

    public event Action<PlayerMotionState, PlayerMotionState> StateChanged;
    public event Action<PlayerMode, PlayerMode> ModeChanged;

    public void SetMode(PlayerMode nextMode, bool force = false)
    {
        if (!force && CurrentMode == nextMode)
        {
            return;
        }

        PlayerMode previousMode = CurrentMode;
        CurrentMode = nextMode;
        ModeChanged?.Invoke(previousMode, nextMode);
    }

    public void ChangeState(PlayerMotionState nextState, bool force = false)
    {
        if (!force && CurrentState == nextState)
        {
            return;
        }

        PlayerMotionState previousState = CurrentState;
        CurrentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }

    public bool IsInState(PlayerMotionState state)
    {
        return CurrentState == state;
    }
}
