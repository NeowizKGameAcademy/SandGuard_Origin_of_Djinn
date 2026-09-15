using UnityEngine;

public class Creature : MonoBehaviour
{
    private CoffinSummon owner;
    private int summonIndex;

    public void Initialize(CoffinSummon coffin, int index)
    {
        owner = coffin;
        summonIndex = index;
    }

    public void Die()
    {
        owner.Respawn(summonIndex);
        gameObject.SetActive(false);
    }

    public void Despawn()
    {
        owner = null;
        gameObject.SetActive(false);
    }
}