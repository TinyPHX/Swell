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

            if (GUILayout.Button("Generate"))
            {
                foreach (SwellMesh swellMesh in swellMeshTargets)
                {
                    swellMesh.GenerateMesh();
                }
            }
        }
    }
}