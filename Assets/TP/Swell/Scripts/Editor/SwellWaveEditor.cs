using System;
using UnityEditor;
using UnityEngine;

namespace Swell.Editors
{
    /**
     * @brief `Editor` for SwellMesh component 
     */
    [CustomEditor(typeof(SwellWave)), CanEditMultipleObjects]
    public class SwellWaveEditor : Editor
    {
        SerializedProperty myProperty;

        private void OnEnable()
        {
            myProperty = serializedObject.FindProperty("customWave");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            SwellWave[] swellWaveTargets = Array.ConvertAll(targets, item => (SwellWave) item);

            bool waveTransformChanged = false;
            foreach (SwellWave wave in swellWaveTargets)
            {
                waveTransformChanged |= wave.transform.hasChanged;

                if (GUI.changed || wave.transform.hasChanged)
                {
                    wave.Update();
                }
                
                wave.transform.hasChanged = false;
            }
            
            foreach (SwellWater water in SwellManager.AllWaters())
            {
                if (GUI.changed || waveTransformChanged)
                {
                    water.Update();
                }
            }
        }
    }
}