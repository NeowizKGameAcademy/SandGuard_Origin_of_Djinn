using UnityEngine;

public class TowerRange : MonoBehaviour
{
    public enum RangeType
    {
        Detect,
        Damage,
        Effect
    }

    [SerializeField] private RangeType rangeType;
    [SerializeField] private TowerController ownerTower;

    public RangeType Type => rangeType;
    public TowerController OwnerTower => ownerTower;

    
}
