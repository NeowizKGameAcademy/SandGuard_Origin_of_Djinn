using UnityEngine;

public class TowerRange : MonoBehaviour
{
    [SerializeField] private GameObject ownerTower;

    public GameObject OwnerTower => ownerTower;
}
