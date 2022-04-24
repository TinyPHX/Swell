using System;
using NWH.DWP2.DefaultWater;
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

            // bool transformChanged = false;
            bool waterMissing = false;
            foreach (SwellMesh swellMesh in swellMeshTargets)
            {
                // transformChanged |= swellMesh.transform.hasChanged;

                if (GUI.changed || swellMesh.transform.hasChanged)
                {
                    if (swellMesh.Water)
                    {
                        swellMesh.Water.EditorUpdate();
                    }
                    else
                    {
                        swellMesh.GenerateMesh();
                        swellMesh.Update();
                    }
                }

                waterMissing |= swellMesh.Water == null;
                
                swellMesh.transform.hasChanged = false;
            }

            if (waterMissing)
            {
                foreach (SwellWater water in SwellManager.AllWaters())
                {
                    if (water.NeedsInitialize())
                    {
                        water.EditorUpdate();
                    }
                }
            }
            
            // bool waterUpdated = false;
            // if (GUI.changed || transformChanged)
            // {
            //     foreach (SwellWater water in SwellManager.GetAll<SwellWater>())
            //     {
            //         water.EditorUpdate();
            //         water.Update();
            //         waterUpdated = true;
            //     }
            // }
            //
            // if (!waterUpdated)
            // {
            //     foreach (SwellMesh swellMesh in swellMeshTargets)
            //     {
            //         if (GUI.changed)
            //         {
            //             swellMesh.GenerateMesh();
            //             swellMesh.Update();
            //         }
            //     }
            // }
        }
    }
}