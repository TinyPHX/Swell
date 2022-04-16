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
        public static void CreateMeshSimple()
        {
            CreateMesh(false, "Swell Mesh (Simple)");
        }

        [MenuItem("GameObject/Swell/Mesh (Levels)", false, 40)]
        public static void CreateMeshLevels()
        {
            CreateMesh(true, "Swell Mesh (Levels)");
        }

        public static void CreateMesh(bool levels, string name)
        {
            SwellMesh swellMesh;
            MeshRenderer meshRenderer = Selected?.GetComponent<MeshRenderer>();
            if (meshRenderer)
            {
                int size = (int)meshRenderer.bounds.size.x;
                swellMesh = Selected.gameObject.AddComponent<SwellMesh>();
                swellMesh.MaxSize = size;
            }
            else
            {
                swellMesh = CreateNew<SwellMesh>(name).GetComponent<SwellMesh>();
                Material material = swellMesh.GetComponent<MeshRenderer>().sharedMaterial;
                if (!material)
                {
                    swellMesh.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"));
                }
            }

            if (levels)
            {
                swellMesh.MaxSize = 0;
                swellMesh.Levels = new []
                {
                    new SwellMesh.MeshLevel(1, 12),
                    new SwellMesh.MeshLevel(2, 4),
                    new SwellMesh.MeshLevel(5, 4),
                    new SwellMesh.MeshLevel(4, 1),
                };
            }
            else
            {
                swellMesh.Levels = new SwellMesh.MeshLevel[] { };
            }
            
            swellMesh.GenerateMesh();
        }

        [MenuItem("GameObject/Swell/Floater", false, 40)]
        public static void CreateFloaterGameObject()
        {
            SwellFloater floater;
            if (Selected && Is<Rigidbody>(Selected.gameObject))
            {
                floater = CreateNew<SwellFloater>("Swell Floater", Selected);
            }
            else
            {
                Transform primitive = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                primitive.gameObject.name = primitive.gameObject.name + " (Floater)";
                if (Selected)
                {
                    primitive.transform.parent = Selected.transform;
                    primitive.transform.localPosition = Vector3.zero;
                }
                floater = primitive.gameObject.AddComponent<SwellFloater>();
            }
            AddRigidbody(floater.gameObject);
            floater.Reset();
        }

        [MenuItem("GameObject/Swell/Floater (Component)", false, 40)]
        public static void CreateFloaterComponent()
        {
            SwellFloater floater = Selected.gameObject.AddComponent<SwellFloater>();
            AddRigidbody(floater.gameObject);
            floater.Reset();
        }

        [MenuItem("GameObject/Swell/Floater (Component)", true)]
        public static bool CreateFloaterGameObjectValidation()
        {
            return Selected != null;
        }

        private static void AddRigidbody(GameObject gameObject)
        {
            if (!Is<Rigidbody>(gameObject))
            {
                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.drag = .2f;
                rigidbody.angularDrag = .2f;
            }
        }

        private static bool Is<T>(GameObject gameObject) where T: Component
        {
            return gameObject != null && (gameObject.GetComponent<T>() != null || gameObject.GetComponentInParent<T>() != null);
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