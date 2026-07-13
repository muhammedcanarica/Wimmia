using System.Collections;
using UnityEngine;

public abstract class OctopusBossAttack : MonoBehaviour
{
    public virtual bool CanRepeatConsecutively => true;

    public virtual void CancelActiveAttack()
    {
    }

    public virtual bool CanUse(OctopusBossController boss)
    {
        return boss != null && !boss.IsDead && isActiveAndEnabled;
    }

    public abstract IEnumerator Execute(OctopusBossController boss);
}
