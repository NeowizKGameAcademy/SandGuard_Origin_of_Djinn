using UnityEngine;

namespace Tower
{
    public sealed class MinionWeaponLoadout : MonoBehaviour
    {
        [SerializeField] private GameObject rightHandWeapon;
        [SerializeField] private GameObject leftHandWeapon;

        private void Awake()
        {
            Equip(rightHandWeapon, "mixamorig:RightHand");
            Equip(leftHandWeapon, "mixamorig:LeftHand");
        }

        private void Equip(GameObject prefab, string boneName)
        {
            if (prefab == null) return;
            Transform bone = FindBone(transform, boneName);
            if (bone == null) return;
            var item = Instantiate(prefab, bone);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }
    }
}
