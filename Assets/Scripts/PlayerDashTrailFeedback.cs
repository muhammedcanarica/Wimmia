using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerDashTrailFeedback : MonoBehaviour
{
    [SerializeField] private TrailRenderer dashTrail;
    [SerializeField] private bool clearTrailWhenDashEnds = true;

    private PlayerStateMachine stateMachine;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();

        if (dashTrail == null)
        {
            dashTrail = GetComponentInChildren<TrailRenderer>(true);
        }

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }
    }

    private void OnEnable()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (stateMachine != null)
        {
            stateMachine.StateChanged += OnStateChanged;
            ApplyState(stateMachine.CurrentState);
        }
    }

    private void OnDisable()
    {
        if (stateMachine != null)
        {
            stateMachine.StateChanged -= OnStateChanged;
        }
    }

    private void OnStateChanged(PlayerMotionState previousState, PlayerMotionState nextState)
    {
        ApplyState(nextState);
    }

    private void ApplyState(PlayerMotionState state)
    {
        if (dashTrail == null)
        {
            return;
        }

        bool shouldEmit = state == PlayerMotionState.Dash;
        dashTrail.emitting = shouldEmit;

        if (!shouldEmit && clearTrailWhenDashEnds)
        {
            dashTrail.Clear();
        }
    }
}
