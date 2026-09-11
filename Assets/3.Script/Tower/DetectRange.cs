using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectRange : MonoBehaviour
{
    [SerializeField] private float Range = 20f;

    public float range => Range;

    [SerializeField] private GameObject Detect_Range_OBJ;

    private void Update()
    {
        Range = Mathf.Clamp(Range, 0f, 50f);
        SetRange();
    }

    private void SetRange()
    {
        if (Detect_Range_OBJ == null)
            return;

        Detect_Range_OBJ.transform.localScale = new Vector3(Range * 2, Detect_Range_OBJ.transform.localScale.y, Range * 2);
    }
}
