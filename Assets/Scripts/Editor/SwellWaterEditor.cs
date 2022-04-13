using System;
using UnityEditor;
using UnityEngine;


namespace Swell.Editors
{
    /**
     * @brief `Editor` for SwellWater component 
     */
    [CustomEditor(typeof(SwellWater)), CanEditMultipleObjects]
    public class SwellWaterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SwellWater[] swellWaterTargets = Array.ConvertAll(targets, item => (SwellWater) item);

            if (GUILayout.Button("Re-initialize"))
            {
                foreach (SwellWater swellWater in swellWaterTargets)
                {
                    swellWater.Reset();
                }
            }
        }
    }
}