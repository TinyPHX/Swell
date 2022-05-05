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
        public string lastTooltip = " ";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            SwellWater[] swellWaterTargets = Array.ConvertAll(targets, item => (SwellWater) item);
            
            foreach (SwellWater water in swellWaterTargets)
            {
                if (GUI.changed || water.transform.hasChanged || water.NeedsInitialize())
                {
                    water.transform.hasChanged = false;
                    water.EditorUpdate();
                }
            }
                
            if (GUILayout.Button(new GUIContent("Refresh", "")))
            {
                foreach (SwellWater swellWater in swellWaterTargets)
                {
                    swellWater.EditorUpdate();
                }
            }
        }
    }
}