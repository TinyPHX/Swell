using System;
using UnityEditor;
using UnityEngine;

namespace Swell.Editors
{
    /**
     * @brief `Editor` for SwellMesh component 
     */
    [CustomEditor(typeof(SwellMesh))]
    public class SwellMeshEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            SwellMesh[] swellMeshTargets = Array.ConvertAll(targets, item => (SwellMesh) item);

            bool anyHasWater = false;
            bool anyTooBig = false;
            foreach (SwellMesh swellMesh in swellMeshTargets)
            {
                if (GUI.changed || swellMesh.transform.hasChanged)
                {
                    swellMesh.EditorUpdate();
                }
                swellMesh.transform.hasChanged = false;

                anyHasWater |= swellMesh.Water != null;
                anyTooBig |= swellMesh.TooBig;
            }

            if (anyTooBig)
            {
                EditorGUILayout.HelpBox("This mesh is too big. See console for details.", MessageType.Warning);
            }

            if (anyHasWater)
            {
                EditorGUILayout.HelpBox("Some values above driven by SwellWater.", MessageType.Info);
            }
        }
    }
}