using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHitStop : MonoBehaviour
{
    private Coroutine activeRoutine;

    public void Trigger(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        float previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousTimeScale;
        activeRoutine = null;
    }
}
