using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MyBox;
using TP.ExtensionMethods;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Swell
{
    /**
     * @brief Creates SwellMesh, applies SwellWave, and provides API to retrieve or modify water.
     * 
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_water.html")]
    public class SwellWater : MonoBehaviour
    {
        [field: Separator("Basic Settings")]
        [field: SerializeField] public Material Material { get; set; } //!< The material applied to the top side of the mesh.
        [field: SerializeField] public Material BottomMaterial { get; set; } //!< The material applied to the bottom side of the mesh.
        [field: SerializeField, Min(0)] public int MeshGridSize { get; set; } = 1; //!< The distance between vertices in your mesh grid.  
        [field: SerializeField, Min(0)] public int MeshSize { get; set; } = 40; //!< The full width of your mesh. If this number is not divisible by MeshGridSize then the actual size will be slightly smaller. 
        [field: Tooltip("Setting this to 0 stops it from modifying the material at all.")]
        [field: SerializeField, OverrideLabel("Texture Size (Main)"), Min(0)] public float MainTextureSize { get; set; } = 0; //!< A constant size to scale the main texture by. This only applies to the Unity standard shader. 
        [field: Tooltip("Setting this to 0 stops it from modifying the material at all.")]
        [field: SerializeField, OverrideLabel("Texture Size (Secondary)"), Min(0)] public float SecondaryTextureSize { get; set; } = 0; //!< A constant size to scale the secondary texture by. This only applies to the Unity standard shader. 
        [field: SerializeField] public ShadowCastingMode CastingMode { get; set; } = ShadowCastingMode.Off; //!< Whether or not this mesh casts shadows.
        [field: SerializeField] public bool ReceiveShadows { get; set; } = false; //!< Whether or not this mesh receives shadows.
        [field: SerializeField] public bool LowPolyNormals { get; set; } = false; //!< Apply low poly normals to this mesh while generating it. 
        
        [field: Separator("Custom Swell Mesh")]
        [field: OverrideLabel(""), SerializeField] private bool useCustomSwellMesh = false; 
        public bool UseCustomSwellMesh { get => useCustomSwellMesh; set => useCustomSwellMesh = value; } //!< When true, the SwellMesh is accessible in the Unity hierarchy giving you access to add custom leveling.
        [field: SerializeField, ConditionalField(nameof(useCustomSwellMesh))] public SwellMesh SwellMesh { get; set; } //!< The SwellMesh being controlled by this SwellWater.

        [Separator("Position Anchoring")]
        [OverrideLabel(""), SerializeField] private bool usePositionAnchor = false;
        public bool UsePositionAnchor { get => usePositionAnchor; set => usePositionAnchor = value; } //!< When true, this can be used to have the SwellMesh follow a target.
        [field: SerializeField, ConditionalField(nameof(usePositionAnchor))] public GameObject PositionAnchor { get; set; } //!< The target for the SwellMesh to anchor to. 
        [field: SerializeField, ConditionalField(nameof(usePositionAnchor))] public Vector2 PositionStep { get; set; } = Vector2.one * 10; //!< The smallest size step the mesh will move at a time. This is useful if your material has texture to it. 
        [field: SerializeField, ConditionalField(nameof(usePositionAnchor))] public bool LockAlbedoPosition { get; set; } //!< When true, this will adjust the standard shader albedo offset to keep a texture in relative place. This is useful for rendering rivers.
        [field: SerializeField, ConditionalField(nameof(usePositionAnchor))] public Transform MeshAlbedoPosition { get; set; } //!< When set this is the the position the albedo will lock to instead of (0, 0).

        [Separator("Optimize")] 
        [OverrideLabel(""), SerializeField] private bool optimize;
        public bool Optimize { get => optimize; set => optimize = value; } //!< When true, water fps throttling is applied to keep SwellWater from hogging resources. 
        [field: SerializeField, ConditionalField(nameof(optimize)), ReadOnly(nameof(autoThrottle))] public float WaterFps { get; private set; } = 60; //!< The number of time the water is updated per frame.
        [SerializeField, ConditionalField(nameof(optimize))] private bool autoThrottle = true;
        public bool AutoThrottle { get => autoThrottle; set => autoThrottle = value; } //!< Then true we'll automatically throttle when the FPS falls below 60.
        [field: SerializeField, ConditionalField(nameof(optimize)), ReadOnly(nameof(autoThrottle), true)] public float ThrottleFps { get; set; } = 60; //!< When the game FPS falls below this number start reducing the WaterFps. 

        private Dictionary<long, float> heightMapDict;
        private bool useHeightMapArray = true; //This was created for debugging and is almost always faster to be set to true.
        private float[][,] heightMapArray;
        private Vector3 position;
        private float gridDensity = 1;
        private bool initialized = false;
        private float lastUpdate;
        private float fps;
        private Material previousMaterial;
        private Material previousBottomMaterial;
        private static readonly int DetailAlbedoMap = Shader.PropertyToID("_DetailAlbedoMap");
        private static readonly int DetailNormalMap = Shader.PropertyToID("_DetailNormalMap");

        public Vector3 Position => position;

        public void Reset()
        {
            if (SwellMesh != null)
            {
                SwellMesh.gameObject.BlowUp();
            }

            Material = new Material(Shader.Find("Standard"));

            EditorUpdate();
        }
        
        void Start()
        {
            position = transform.position;
            
            this.Register();
            InitializeWaterMesh();
            
            heightMapDict = new Dictionary<long, float>();
            SwellMesh.MeshLevel[] levels = SwellMesh.Levels;
            heightMapArray = new float[levels.Length][,];

            for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
            {
                int levelSize = levels[levelIndex].VertWidth;
                heightMapArray[levelIndex] = new float[levelSize,levelSize];
            }
        }

        private void Initialize(bool force = false)
        {
            if (force || NeedsInitialize())
            {
                initialized = true;
                Start();
            }
        }

        public bool NeedsInitialize()
        {
            return !initialized || heightMapArray == null || heightMapDict == null;
        }

        public void EditorUpdate()
        {
            foreach (SwellWave wave in SwellManager.AllWaves())
            {
                if (wave)
                {
                    wave.Update();
                }
            }
            
            Initialize(!Application.isPlaying);
            Update();

            foreach (SwellMesh childSwellMesh in GetComponentsInChildren<SwellMesh>())
            {
                childSwellMesh.gameObject.hideFlags = HideFlags.None;
            }
            SwellMesh.gameObject.hideFlags = UseCustomSwellMesh ? HideFlags.None : HideFlags.HideInHierarchy;
        }

        public bool ShouldUpdate()
        {
            bool shouldUpdate = true;

            if (optimize && Application.isPlaying)
            {
                if (AutoThrottle)
                {
                    //fps is an approximate rolling average across 10 frames.
                    fps = fps - fps / 10 +  1 / Time.deltaTime / 10;
                    
                    if (fps < ThrottleFps && WaterFps >= 1 || fps > ThrottleFps && WaterFps < 60)
                    {
                        WaterFps += .1f * (fps - WaterFps);
                    }
                }

                if (WaterFps > 0 && Time.time - lastUpdate < 1 / WaterFps)
                {
                    shouldUpdate = false;
                }
            }

            return shouldUpdate;
        }
        
        public void Update()
        {
            if (ShouldUpdate())
            {
                lastUpdate = Time.time;
                Vector3 currentPosition = transform.position;
                bool positionChanged = false;
                if (usePositionAnchor && PositionAnchor)
                {
                    Vector3 anchor = PositionAnchor.transform.position;
                    Vector3 delta = anchor - currentPosition;

                    if (Mathf.Abs(delta.x) > PositionStep.x)
                    {
                        currentPosition.x += PositionStep.x * (int)(delta.x / PositionStep.x);
                        positionChanged = true;
                    }
                    
                    if (Mathf.Abs(delta.z) > PositionStep.y)
                    {
                        currentPosition.z += PositionStep.y * (int)(delta.z / PositionStep.y);
                        positionChanged = true;
                    }
                }

                if (positionChanged)
                {
                    transform.position = currentPosition;
                    position = currentPosition;
                    
                    foreach (Material material in SwellMesh.Materials)
                    {
                        UpdateTexture(material);
                    }
                }

                Initialize();
                UpdateWater();
            }
        }

        void InitializeWaterMesh(bool setDefaults=false)
        {
            if (SwellMesh == null)
            {
                SwellMesh = GetComponentInChildren<SwellMesh>();

                if (SwellMesh != null)
                {
                    Material = SwellMesh.Materials[0];
                    MeshGridSize = SwellMesh.StartGridSize;
                    MeshSize = SwellMesh.MaxSize;
                    LowPolyNormals = SwellMesh.LowPolyNormals;
                }
            }
            
            if (SwellMesh == null)
            {
                Type[] components = { typeof(SwellMesh) };
                SwellMesh = new GameObject("Swell Mesh", components).GetComponent<SwellMesh>();
                SwellMesh.transform.parent = transform;
                SwellMesh.transform.localPosition = Vector3.zero;
                SwellMesh.Material = new Material(Shader.Find("Standard"));
            }

            UpdateMaterials();

            SwellMesh.Water = this;

            if (MeshSize > 0) SwellMesh.MaxSize = MeshSize;
            if (MeshGridSize > 0) SwellMesh.StartGridSize = MeshGridSize;
            SwellMesh.Renderer.shadowCastingMode = CastingMode;
            SwellMesh.Renderer.receiveShadows = ReceiveShadows;
            SwellMesh.LowPolyNormals = LowPolyNormals;
            SwellMesh.Top = Material != null;
            SwellMesh.Bottom = BottomMaterial != null;

            
            if (!useCustomSwellMesh)
            {
                SwellMesh.Levels = new SwellMesh.MeshLevel[] { };
            }

            SwellMesh.GenerateMesh();
        }

        private void UpdateMaterials()
        {
            List<Material> materials = new List<Material>();
            if (Material)
            {
                UpdateTexture(Material);
                materials.Add(Material);
            }
            if (BottomMaterial)
            {
                UpdateTexture(BottomMaterial);
                materials.Add(BottomMaterial);
            }
            SwellMesh.Materials = materials.ToArray();
        }

        private void UpdateTexture(Material material)
        {
            if (material != null && material.shader.name == "Standard")
            {
                SwellMesh.Update();
                
                if (MainTextureSize > 0)
                {
                    float newScale = SwellMesh.Size / MainTextureSize;
                    Vector2 newOffset = Vector2.one *  (-newScale / 2 + .5f);
                    material.mainTextureScale =  Vector2.one * newScale;

                    if (LockAlbedoPosition)
                    {
                        newOffset += new Vector2(
                            SwellMesh.Renderer.transform.position.x - MeshAlbedoPosition?.position.x ?? 0,
                            SwellMesh.Renderer.transform.position.z - MeshAlbedoPosition?.position.z ?? 0
                        ) / 2;
                        newOffset /= MainTextureSize / 2;
                        newOffset += Vector2.one * (MainTextureSize - SwellMesh.Size) / MainTextureSize / 2;
                    }
                    
                    material.mainTextureOffset = newOffset;
                    
                }

                if (SecondaryTextureSize > 0)
                {
                    float newScale = SwellMesh.Size / SecondaryTextureSize;
                    float newOffset = -newScale / 2 + .5f;
                    
                    material.SetTextureScale(DetailAlbedoMap, Vector2.one * newScale);
                    material.SetTextureScale(DetailAlbedoMap, Vector2.one * newScale);
                    material.SetTextureOffset(DetailAlbedoMap, Vector2.one * newOffset);
                    material.SetTextureOffset(DetailAlbedoMap, Vector2.one * newOffset);
                }
            }
        }

        public void ClearMaterial()
        {
            previousMaterial = Material;
            Material = null;
        }

        public void RestoreMaterial()
        {
            if (Material == null)
            {
                if (previousMaterial == null)
                {
                    previousMaterial = new Material(Shader.Find("Standard"));
                }
                Material = previousMaterial;
            }
        }

        public void ClearBottomMaterial()
        {
            previousBottomMaterial = BottomMaterial;
            BottomMaterial = null;
        }

        public void RestoreBottomMaterial()
        {
            if (BottomMaterial == null)
            {
                if (previousBottomMaterial == null)
                {
                    previousBottomMaterial = new Material(Shader.Find("Standard"));
                }
                
                BottomMaterial = previousBottomMaterial;
            }
        }
        
        
        private void OnDestroy()
        {
            this.UnRegister();
        }
        
        void UpdateWater()
        {   
            UpdateHeightMap();
            UpdateMeshes();
        }

        /**
         * @brief Gets the exact height of the SwellMesh 
         *
         * Returns y intersection value of the water mesh at some x, z value. To get the exact position of the
         * mesh we get the height of the nearest vectors and then interpolate between them.
         * 
         * @param position The position (x, z) at which to check the water height.
         *
         * ##Example
         * Returns the height of the water at the origin of the scene:
         * ```
         * GetWaterHeight(Vector3.Zero) 
         * ```
         *
         * Providing the levelIndex was more efficient but we don't know for sure if the level has this point. 
         * float cc = GetHeight(maxX, maxY, li: levelIndex);
         * float ff = GetHeight(minX, minY, li: levelIndex);
         * float cf = GetHeight(maxX, minY, li: levelIndex);
         * float fc = GetHeight(minX, maxY, li: levelIndex);
         */
        public float GetWaterHeight(Vector3 position)
        {
            float waterHeight = 0;
            
            int levelIndex = PositionToLevelIndex(position.x, position.z);
            if (levelIndex == -1)
            {
                //Position is off of the grid and there is no need to get grid corners and interpolate
                waterHeight = GetHeight(position.x, position.z);
            }
            else
            {
                SwellMesh.MeshLevel meshLevel = SwellMesh.Levels[levelIndex];
                float levelStep = meshLevel.Step;

                float gridX = PositionToGridX(position.x, meshLevel);
                float gridY = PositionToGridY(position.z, meshLevel);

                float minX = gridX;
                float maxX = gridX + levelStep;
                float minY = gridY;
                float maxY = gridY + levelStep;

                //Get corners.
                float cc = GetHeight(maxX, maxY);
                float ff = GetHeight(minX, minY);
                float cf = GetHeight(maxX, minY);
                float fc = GetHeight(minX, maxY); 

                float ratioX = (position.x - minX) / levelStep;
                float ratioZ = (position.z - minY) / levelStep;

                //interpolate between corners
                float x1 = cc + (cf - cc) * (1 - ratioZ);
                float x2 = fc + (ff - fc) * (1 - ratioZ);
                waterHeight = x1 + (x2 - x1) * (1 - ratioX);
            }

            return waterHeight;
        }

        /**
         * @brief Gets the height of a nearby Vector on the SwellMesh
         * 
         * Returns y intersection value of the water mesh at some x, z value. This is an optimized approximation
         * that only gets the height of a nearby vector. 
         *
         * @param position The position (x, z) at which to check the water height.
         */
        public float GetWaterHeightOptimized(Vector3 position)
        {
            return GetHeight(Mathf.CeilToInt(position.x), Mathf.CeilToInt(position.z)); // TODO This needs to round to grid. Not to int. This will break fast depth method. 
        }
        
        /**
         * @brief Returns exact wave height of the water mesh at some x, z value.
         *
         * This will check first to see if the position height has been calculated this frame, if not it will calculate it as long as calculate is set to
         * true. li is the level of fidelity used in the SwellMesh.
         *
         * @param xPosition x position on the mesh
         * @param yPosition y position on the mesh (z position in unity global space)
         * @param calculate If false this will only return heights that have already been calculated this frame.
         * @param li The grid level (fidelity) used to get the position from
         */
        public float GetHeight(float xPosition, float yPosition, bool calculate=true, int li = -1)
        {
            float height = 0;

            if (useHeightMapArray)
            {
                // Get height from height map array but fall back to calculating height and storing it in dictionary if 
                // it's not in the bounds of the array.
                int xi, yi;
                
                if (li == -1)
                {
                    (li, xi, yi) = PositionToArrayIndexes(xPosition, yPosition);
                }
                else
                {
                    xi = PositionToIndexX(xPosition, SwellMesh.Levels[li]);
                    yi = PositionToIndexY(yPosition, SwellMesh.Levels[li]);
                }
                
                if (heightMapArray != null && xi >= 0 && xi < heightMapArray[li].Length && yi >= 0 && yi < heightMapArray[li].GetLength(1))
                {
                    height = heightMapArray[li][xi,yi];
                }
                else if (calculate)
                {
                    var key = PositionToHeightKey(xPosition, yPosition);

                    if (!heightMapDict.TryGetValue(key, out height))
                    {
                        height = CalculateHeight(xPosition, yPosition);
                    }
                }
            }
            else
            {
                var key = PositionToHeightKey(xPosition, yPosition);

                if (!heightMapDict.TryGetValue(key, out height) && calculate)
                {
                    height = CalculateHeight(xPosition, yPosition);
                }
            }
            
            return height;
        }

        public void SetHeight(float xPosition, float yPosition, float height)
        {
            if (useHeightMapArray)
            {
                int li, xi, yi;
                (li, xi, yi) = PositionToArrayIndexes(xPosition, yPosition);
                heightMapArray[li][xi,yi] = height;
            }
            else
            {
                var key = PositionToHeightKey(xPosition, yPosition);
                heightMapDict[key] = height;
            }
        }

        public void AddHeight(float xPosition, float yPosition, float height)
        {
            if (useHeightMapArray)
            {
                int xi = PositionToIndexX(xPosition);
                int yi = PositionToIndexY(yPosition);
                
                heightMapArray[0][xi,yi] += height;
            }
            else
            {
                var key = PositionToHeightKey(xPosition, yPosition);
                heightMapDict[key] += height;
            }
        }

        long PositionToHeightKey(float xPosition, float yPosition)
        {
            //Szudzik's pairing function
            //https://stackoverflow.com/questions/919612/mapping-two-integers-to-one-in-a-unique-and-deterministic-way

            int x = PositionToIndexX(xPosition);
            int y = PositionToIndexY(yPosition);
            
            var A = (ulong)(x >= 0 ? 2 * (long)x : -2 * (long)x - 1);
            var B = (ulong)(y >= 0 ? 2 * (long)y : -2 * (long)y - 1);
            var C = (long)((A >= B ? A * A + A + B : A + B * B) / 2);
            return x < 0 && y < 0 || x >= 0 && y >= 0 ? C : -C - 1;
            
            // Used this to debug optimizations. Can be deleted if you see it in the future and shit aint slow.
            // return (int)(xPosition * gridDensity + 1) + 1000 * (int)(yPosition * gridDensity + 1);
        }

        (int, int, int) PositionToArrayIndexes(float positionX, float positionY)
        {
            SwellMesh.MeshLevel level = null;
            SwellMesh.MeshLevel[] levels = SwellMesh.Levels;
            for (int li = 0; li < levels.Length; li++)
            {
                level = levels[li];
                if (level.InBounds(positionX - position.x, positionY - position.z))
                {
                    int xi = PositionToIndexX(positionX, level);
                    int yi = PositionToIndexY(positionY, level);
                    return (li, xi, yi);
                }
            }

            return (-1, -1, -1);
        }
        
        int PositionToLevelIndex(float positionX, float positionY)
        {
            SwellMesh.MeshLevel[] levels = SwellMesh.Levels;
            for (int li = 0; li < levels.Length; li++)
            {
                if (levels[li].InBounds(positionX - position.x, positionY - position.z))
                {
                    return li;
                }
            }
        
            return -1;
        }
        
        SwellMesh.MeshLevel PositionToLevel(float positionX, float positionY)
        {
            SwellMesh.MeshLevel[] levels = SwellMesh.Levels;
            for (int li = 0; li < levels.Length; li++)
            {
                if (levels[li].InBounds(positionX - position.x, positionY - position.z))
                {
                    return levels[li];
                }
            }
        
            return null;
        }

        float GetPositionLevelOffset(float positionX, float positionY)
        {
            return (PositionToLevel(positionX, positionY)?.VertWidth ?? 2) % 2 == 0 ? .5f : 1;
        }
        
        float PositionToGridX(float positionX, SwellMesh.MeshLevel level)
        {
            return positionX - (positionX + level.Offset - position.x) % level.Step;
        }
        
        float PositionToGridY(float positionY, SwellMesh.MeshLevel level)
        {   
            return positionY - (positionY + level.Offset - position.z) % level.Step;
        }
        
        int PositionToIndexX(float positionX, SwellMesh.MeshLevel level)
        {
            return (int) ((positionX + level.Offset - position.x) / level.Step);
        }
        
        int PositionToIndexY(float positionY, SwellMesh.MeshLevel level)
        {
            return (int) ((positionY + level.Offset - position.z) / level.Step);
        }
        
        int PositionToIndexX(float positionX)
        {
            return PositionToIndex(positionX, position.x);
        }

        int PositionToIndexY(float positionY)
        {
            return PositionToIndex(positionY, position.z);
        }

        float IndexToPositionX(int indexX, SwellMesh.MeshLevel level)
        {
            return (indexX - .5f) * level.Step - level.Offset + position.x;
        }
        
        float IndexToPositionY(int indexY, SwellMesh.MeshLevel level)
        {
            return (indexY - .5f) * level.Step - level.Offset + position.z;
        }

        int PositionToIndex(float position, float parentPosition)
        {
            return (int) ((position + SwellMesh.Offset - parentPosition) * gridDensity + .5f);
        }

        float IndexToPositionX(int index)
        {
            return IndexToPosition(index, position.x);
        }
        
        float IndexToPositionY(int index)
        {
            return IndexToPosition(index, position.z);
        }
        
        float IndexToPosition(int index, float parentPosition)
        {
            return (index - .5f) / gridDensity - SwellMesh.Offset + parentPosition;
        }
        
        int PositionToGrid(float position, float offset)
        {
            return (int)(position * gridDensity + offset);
        }

        float CalculateHeight(float xPosition, float yPosition, List<SwellWave> waves = null)
        {
            float levelOffset = GetPositionLevelOffset(xPosition, yPosition);
            int x = PositionToGrid(xPosition, levelOffset);
            int y = PositionToGrid(yPosition, levelOffset);

            float totalHeight = 0;

            waves ??= SwellManager.AllWaves();
            
            for (int i = 0; i < waves.Count; i++)
            {
                totalHeight += waves[i].GetHeight(x, y);
            }

            return totalHeight;
        }

        void UpdateHeightMap()
        {
            foreach (Vector3 point in SwellMesh.Mesh.vertices)
            {
                // Vector3 transformedPoint = swellMesh.transform.TransformPoint(point); // This was causing out of bounds index error on mesh movement
                Vector3 transformedPoint = point + position;
                float newHeight = CalculateHeight(transformedPoint.x, transformedPoint.z);
                SetHeight(transformedPoint.x, transformedPoint.z, newHeight);
            }
        }

        void UpdateMeshes(List<SwellWave> waves=null)
        {
            // Alternative to calling transformPoint on every point. This only works without scale
            // Vector3 startPosition = meshFilters[0].mesh.vertices[0];
            // Vector3 positionChange = startPosition - transform.TransformPoint(startPosition);
            
            Mesh mesh = SwellMesh.Mesh;
            Vector3[] vertices = mesh.vertices;
            
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = SwellMesh.transform.TransformPoint(vertices[i]);
                float waveHeight = GetHeight(v.x, v.z, true);

                vertices[i] = new Vector3(
                    vertices[i].x,
                    waveHeight,
                    vertices[i].z
                );
            }

            mesh.vertices = vertices;
            
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                if (!UseCustomSwellMesh)
                {
                    float fullSize = MeshSize;
                    Gizmos.color = new Color(.6f, .6f, .8f);
                    Gizmos.DrawWireCube(transform.position, new Vector3(fullSize, 0, fullSize));
                }
            }
        }
    }
}