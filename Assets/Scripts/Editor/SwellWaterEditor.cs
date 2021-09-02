using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SwellWater))]
public class SwellWaterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Re-initialize"))
        {
            SwellWater[] swellWaterTargets = Array.ConvertAll(targets, item => (SwellWater) item);

            foreach (SwellWater swellWater in swellWaterTargets)
            {
                swellWater.Reset();
            }
        }
    }
}