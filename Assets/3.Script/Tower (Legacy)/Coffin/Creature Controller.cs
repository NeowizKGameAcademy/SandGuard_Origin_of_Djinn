using UnityEngine;

public class CreatureController : MonoBehaviour
{
    protected CoffinSummon owner;
    protected int summonIndex;
    protected Vector3 summonPoint;

    protected bool IsInitialized;

    public void Initialize(CoffinSummon coffin, int index, Vector3 point)
    {
        owner = coffin;
        summonIndex = index;
        summonPoint = point;

        IsInitialized = true;
    }

    public void Despawn()
    {
        IsInitialized = false;
        owner = null;

        gameObject.SetActive(false);
    }
}