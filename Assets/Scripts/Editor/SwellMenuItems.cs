using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Swell.Editors
{
    /**
     * @brief `Right Click > Create` context menu items for quick shorcuts to create Swell GameObjects
     */
    public class SwellMenuItems
    {
        [MenuItem("GameObject/Swell/Water", false, 40)]
        public static void CreateWater()
        {
            CreateNew<SwellWater>("Swell Water");
        }

        [MenuItem("GameObject/Swell/Wave (Rounded)", false, 40)]
        public static void CreateWaveSin()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Rounded)");
            wave.WaveType = SwellWave.Type.rounded;
        }

        [MenuItem("GameObject/Swell/Wave (Pointed)", false, 40)]
        public static void CreateWaveRootSin()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Pointed)");
            wave.WaveType = SwellWave.Type.pointed;
        }

        [MenuItem("GameObject/Swell/Wave (Random)", false, 40)]
        public static void CreateWavePerlin()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Random)");
            wave.WaveType = SwellWave.Type.random;
        }

        [MenuItem("GameObject/Swell/Wave (Bell)", false, 40)]
        public static void CreateWaveGausian()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Bell)");
            wave.WaveType = SwellWave.Type.bell;
        }

        [MenuItem("GameObject/Swell/Wave (Ripple)", false, 40)]
        public static void CreateWaveRadial()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Ripple)");
            wave.WaveType = SwellWave.Type.ripple;
        }

        [MenuItem("GameObject/Swell/Wave (Custom)", false, 40)]
        public static void CreateWaveCustom()
        {
            SwellWave wave = CreateNew<SwellWave>("Swell Wave (Custom)");
            wave.WaveType = SwellWave.Type.custom;
        }

        [MenuItem("GameObject/Swell/Mesh", false, 40)]
        public static void CreateMesh()
        {
            SwellMesh swellMesh = CreateNew<SwellMesh>("Swell Mesh").GetComponent<SwellMesh>();
            swellMesh.GenerateMesh();
            swellMesh.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"));
        }

        [MenuItem("GameObject/Swell/Floater", false, 40)]
        public static void CreateFloater()
        {
            Transform parent = null;
            Rigidbody selectedRigidBody = Selected?.GetComponent<Rigidbody>();
            SwellFloater floater = null;
            if (selectedRigidBody)
            {
                floater = selectedRigidBody.gameObject.AddComponent<SwellFloater>();
            }
            else
            {
                parent = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                if (Selected)
                {
                    parent.transform.parent = Selected.transform;
                    parent.transform.localPosition = Vector3.zero;
                }

                Rigidbody rigidbody = parent.gameObject.AddComponent<Rigidbody>();
                rigidbody.drag = .2f;
                rigidbody.angularDrag = .2f;
                parent.gameObject.name = parent.gameObject.name + " (Floater)";
                floater = CreateNew<SwellFloater>("Swell Floater", parent);
            }

            floater.Reset();
        }

        private static T CreateNew<T>(string name, Transform parent = null) where T : Component
        {
            if (parent == null)
            {
                parent = Selected;
            }

            T newComponent = new GameObject(name, new Type[] {typeof(T)}).GetComponent<T>();
            newComponent.transform.parent = parent;
            newComponent.transform.localPosition = Vector3.zero;

            return newComponent;
        }

        private static Transform Selected
        {
            get
            {
                Transform selected = null;

                if (Selection.transforms != null && Selection.transforms.Length > 0)
                {
                    selected = Selection.transforms.First();
                }

                return selected;
            }
        }
    }
}