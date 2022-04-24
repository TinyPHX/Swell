using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MyBox;
using NWH.DWP2.DefaultWater;
using TP.ExtensionMethods;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace Swell
{
    /**
     * @brief Creates SwellMesh, applies SwellWave s, and provides API to retrieve or modify water.
     * 
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_water.html")]
    public class SwellWater : MonoBehaviour
    {
        [Separator("Basic Settings")] 
        [SerializeField] private Material material; //!< TODO
        
        [Separator("Mesh Settings")] 
        [SerializeField] private bool customSwellMesh = false; //!< TODO: This is how you add member docs in doxigen.
        [SerializeField, ConditionalField(nameof(customSwellMesh), true), Min(1)] private int meshGridSize = 1; //!< TODO
        [SerializeField, ConditionalField(nameof(customSwellMesh), true), Min(1)] private int meshSize = 40; //!< TODO
        [SerializeField, ConditionalField(nameof(customSwellMesh))] private SwellMesh swellMesh; //!< TODO
        [SerializeField] private float mainTextureSize = 0; //!< TODO
        [SerializeField] private int secondaryTextureSize = 0; //!< TODO
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off; //!< TODO
        [SerializeField] private bool receiveShadows = false; //!< TODO
        
        [Separator("Calculate Normals")] 
        [OverrideLabel("")]
        [SerializeField] private bool calculateNormals = true; //!< TODO
        [SerializeField, ConditionalField(nameof(calculateNormals))] private bool lowPolyNormals = false; //!< TODO
        [SerializeField, ConditionalField(nameof(calculateNormals))] private float calculateNormalsFrequency = 1; //!< TODO (in seconds)
        private float lastCalculateNormalsTime = 0; //!< TODO
        
        [Separator("Position Anchoring")] 
        [OverrideLabel("")]
        [SerializeField] private bool usePositionAnchor = false; //!< TODO
        [SerializeField, ConditionalField(nameof(usePositionAnchor))] private GameObject positionAnchor; //!< TODO
        [SerializeField, ConditionalField(nameof(usePositionAnchor))] private Vector2 positionStep = Vector2.one * 10; //!< TODO
        [SerializeField, ConditionalField(nameof(usePositionAnchor))] private bool lockAlbedoPosition; //!< TODO
        [SerializeField, ConditionalField(nameof(usePositionAnchor))] private Transform meshAlbedoPosition; //!< TODO

        [Separator("Optimize")] 
        [OverrideLabel("")]
        [SerializeField] private bool optimize; //!< TODO
        [SerializeField, ConditionalField(nameof(optimize)), ReadOnly(nameof(autoThrottle))] private float waterFps = 60; //!< TODO
        [SerializeField, ConditionalField(nameof(optimize))] private bool autoThrottle = true; //!< TODO
        [SerializeField, ConditionalField(nameof(optimize)), ReadOnly(nameof(autoThrottle), true)] private float throttleFps = 60; //!< TODO If the game fps falls under this fps then the waterFPS will automatically drop.
        
        private Dictionary<long, float> heightMapDict;
        private bool useHeightMapArray = true; //This was created for debugging and is almost always faster to be set to true.
        private float[][,] heightMapArray;
        private Vector3 position;
        private float gridDensity = 1;
        private bool initialized = false;
        private float lastUpdate;
        private float fps;

        public Vector3 Position => position;

        public Material Material
        {
            get { return material; }
            set { material = value; }
        }

        public void Reset()
        {
            if (swellMesh != null)
            {
                swellMesh.gameObject.BlowUp();
            }

            EditorUpdate();
        }
        
        void Start()
        {
            position = transform.position;
            
            this.Register();
            InitializeWaterMesh();
            
            heightMapDict = new Dictionary<long, float>();
            SwellMesh.MeshLevel[] levels = swellMesh.Levels;
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
            
            Initialize(true);
            UpdateMeshLowPoly(true);
            Update();
        }

        public bool ShouldUpdate()
        { 
            bool shouldUpdate = true;

            if (optimize && Application.isPlaying)
            {
                if (autoThrottle)
                {
                    //fps is an approximate rolling average across 10 frames.
                    fps = fps - fps / 10 +  1 / Time.deltaTime / 10;
                    
                    if (fps < throttleFps && waterFps >= 1 || fps > throttleFps && waterFps < 60)
                    {
                        waterFps += .1f * (fps - waterFps);
                    }
                }

                if (waterFps > 0 && Time.time - lastUpdate < 1 / waterFps)
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
                if (usePositionAnchor && positionAnchor)
                {
                    Vector3 anchor = positionAnchor.transform.position;

                    transform.position = new Vector3(
                        anchor.x - anchor.x % positionStep.x,
                        transform.position.y,
                        anchor.z - anchor.z % positionStep.y
                    );
                }

                position = transform.position;
                Initialize();
                UpdateWater();
            }
        }

        void InitializeWaterMesh(bool setDefaults=false)
        {
            if (swellMesh == null)
            {
                Type[] components = { typeof(SwellMesh) };
                swellMesh = new GameObject("Swell Mesh", components).GetComponent<SwellMesh>();
                swellMesh.transform.parent = transform;
                swellMesh.transform.localPosition = Vector3.zero;
                swellMesh.Material = new Material(Shader.Find("Standard"));

                setDefaults = true;
            }

            swellMesh.Water = this;

            if (setDefaults || !customSwellMesh)
            {
                swellMesh.MaxSize = meshSize;
                swellMesh.StartGridSize = meshGridSize;
                swellMesh.Levels = new SwellMesh.MeshLevel[] { };
            }

            swellMesh.Renderer.shadowCastingMode = shadowCastingMode;
            swellMesh.Renderer.receiveShadows = receiveShadows;
                
            if (material)
            {
                if (material.shader.name == "Standard")
                {
                    if (mainTextureSize > 0)
                    {
                        material.mainTextureScale =
                            Vector2.one * (swellMesh.Size / (float) mainTextureSize);
                    }

                    if (secondaryTextureSize > 0)
                    {
                        material.SetTextureScale("_DetailAlbedoMap",
                            Vector2.one * (swellMesh.Size / (float) secondaryTextureSize));
                        material.SetTextureScale("_DetailNormalMap",
                            Vector2.one * (swellMesh.Size / (float) secondaryTextureSize));
                    }
                }

                swellMesh.Material = material;

                if (swellMesh.Material.shader.name == "Standard")
                {
                    swellMesh.Update();
                    if (mainTextureSize > 0)
                    {
                        swellMesh.Material.mainTextureScale =
                            Vector2.one * (swellMesh.Size / (float) mainTextureSize);
                    }

                    if (secondaryTextureSize > 0)
                    {
                        swellMesh.Material.SetTextureScale("_DetailAlbedoMap",
                            Vector2.one * (swellMesh.Size / (float) secondaryTextureSize));
                        swellMesh.Material.SetTextureScale("_DetailNormalMap",
                            Vector2.one * (swellMesh.Size / (float) secondaryTextureSize));
                    }
                }
            }

            swellMesh.GenerateMesh();
        }
        
        private void OnDestroy()
        {
            this.UnRegister();
        }
        
        void UpdateWater()
        {   
            UpdateHeightMap();
            UpdateMeshes();
            UpdateMeshBoundsAndNormals();
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
                SwellMesh.MeshLevel meshLevel = swellMesh.Levels[levelIndex];
                float levelStep = meshLevel.Step;

                float gridX = PositionToGridX(position.x, meshLevel);
                float gridY = PositionToGridY(position.z, meshLevel);

                float minX = gridX;
                float maxX = gridX + levelStep;
                float minY = gridY;
                float maxY = gridY + levelStep;

                //Get corners. TODO: We probably interpolate between 3 vectors instead of 4.
                float cc = GetHeight(maxX, maxY, li: levelIndex);
                float ff = GetHeight(minX, minY, li: levelIndex);
                float cf = GetHeight(maxX, minY, li: levelIndex);
                float fc = GetHeight(minX, maxY, li: levelIndex);

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
                    xi = PositionToIndexX(xPosition, swellMesh.Levels[li]);
                    yi = PositionToIndexY(yPosition, swellMesh.Levels[li]);
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
            SwellMesh.MeshLevel[] levels = swellMesh.Levels;
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
            SwellMesh.MeshLevel[] levels = swellMesh.Levels;
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
            SwellMesh.MeshLevel[] levels = swellMesh.Levels;
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
            return (int) ((position + swellMesh.Offset - parentPosition) * gridDensity + .5f);
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
            return (index - .5f) / gridDensity - swellMesh.Offset + parentPosition;
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
            if (lockAlbedoPosition)
            {
                if (swellMesh.Material.shader.name == "Standard")
                {
                    Vector2 offset = new Vector2(
                        swellMesh.Renderer.transform.position.x - meshAlbedoPosition?.position.x ?? 0,
                        swellMesh.Renderer.transform.position.z - meshAlbedoPosition?.position.z ?? 0
                    );

                    offset /= swellMesh.Size;
                    swellMesh.Material.mainTextureOffset = offset;
                }
            }
            
            foreach (Vector3 point in swellMesh.Mesh.vertices)
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
            
            Mesh mesh = swellMesh.Mesh;
            Vector3[] vertices = mesh.vertices;
            
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = swellMesh.transform.TransformPoint(vertices[i]);
                float waveHeight = GetHeight(v.x, v.z, true);

                vertices[i] = new Vector3(
                    vertices[i].x,
                    waveHeight,
                    vertices[i].z
                );
            }

            mesh.vertices = vertices;

            UpdateMeshLowPoly();
        }

        void UpdateMeshLowPoly(bool force = false)
        {
            if (calculateNormals)
            {
                if (Time.time - lastCalculateNormalsTime > calculateNormalsFrequency || force)
                {
                    lastCalculateNormalsTime = Time.time;

                    if (lowPolyNormals)
                    {
                        Mesh meshLowPoly = swellMesh.Mesh;

                        if (meshLowPoly.vertices.Length != meshLowPoly.triangles.Length)
                        {
                            int indexCount = meshLowPoly.triangles.Length;
                            int maxVertCount = 65535; //https://docs.unity3d.com/ScriptReference/Mesh-indexFormat.html
                            if (indexCount > maxVertCount)
                            {
                                Debug.LogWarning("Generating low poly normals require that many more vectors are added to our your SwellMesh. Unity has a limit of 65,000" +
                                                 "vectors per mesh and this mesh has exceeded that limit. To create a larger low poly water you'll have to create multiple SwellWaters." +
                                                 "https://docs.unity3d.com/ScriptReference/Mesh.SetIndexBufferParams.html");
                            }

                            Vector3[] verts1 = meshLowPoly.vertices;
                            Vector2[] uv1 = meshLowPoly.uv;
                            int[] tris1 = meshLowPoly.triangles;
                            Vector4[] tang1 = meshLowPoly.tangents;
                            Vector3[] normal1 = meshLowPoly.normals;

                            int newLength = tris1.Length; //Every tri needs dedicated verts not shared with any other tri

                            Vector3[] verts2 = new Vector3[newLength];
                            Vector2[] uv2 = new Vector2[newLength];
                            int[] tris2 = new int[newLength];
                            Vector4[] tang2 = new Vector4[newLength];
                            Vector3[] normal2 = new Vector3[newLength];

                            for (int newIndex = 0; newIndex < newLength; newIndex++)
                            {
                                int oldIndex = tris1[newIndex];
                                tris2[newIndex] = newIndex;
                                verts2[newIndex] = verts1[oldIndex];
                                uv2[newIndex] = uv1[oldIndex];
                                tang2[newIndex] = tang1[oldIndex];
                                normal2[newIndex] = normal1[oldIndex];
                            }

                            meshLowPoly.vertices = verts2;
                            meshLowPoly.uv = uv2;
                            meshLowPoly.tangents = tang2;
                            meshLowPoly.normals = normal2;
                            meshLowPoly.triangles = tris2;
                        }
                    }
                }
            }
        }
        
        void UpdateMeshBoundsAndNormals()
        {
            swellMesh.Mesh.RecalculateBounds();
            if (calculateNormals)
            {
                swellMesh.Mesh.RecalculateNormals();
            }
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                if (!customSwellMesh)
                {
                    float fullSize = meshSize;
                    Gizmos.color = new Color(.6f, .6f, .8f);
                    Gizmos.DrawWireCube(transform.position, new Vector3(fullSize, 0, fullSize));
                }
            }
        }
    }
}