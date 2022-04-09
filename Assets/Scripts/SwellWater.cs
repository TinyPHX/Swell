using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Swell
{
    public class SwellWater : MonoBehaviour
    {
        private const string H1 = " ";

        [Header(H1 + "Mesh" + H1)] 
        [SerializeField] private bool useSwellMesh = false;
        [SerializeField] private SwellMesh swellMesh;
        [SerializeField] private float mainTextureSize = 0;
        [SerializeField] private int secondaryTextureSize = 0;
        [SerializeField] private MeshFilter meshFilterPrefab;
        [SerializeField] private Material material;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
        [SerializeField] private bool receiveShadows = false;
        [SerializeField] private bool lockAlbedoPosition;
        [SerializeField] private Transform meshAlbedoPosition;
        
        private GameObject meshFilterGrid;
        private bool combineMeshes = true;
        private List<MeshFilter> meshFiltersLowPoly;
        private MeshFilter mergedMeshFilter;
        private List<MeshFilter> meshFilters;
        private List<List<int>> overlappingVectors;
        private Bounds meshBounds;
        private bool meshMoved = true;
        private Vector3 position;
        
        [Header(H1+"Basic Settings"+H1)]
        [SerializeField] private int gridSize = 10; //number of tiles in water square
        private float gridDensity = 1; //distance between vectors
        private int tileWidth = 10; //Width of each tile
        private float centerOffset;
        [SerializeField] private bool remapUvs = false;

        [Header(H1+"Water Horizon"+H1)]
        [SerializeField] private bool showWaterHorizon;
        [SerializeField] private Material horizonMaterial;
        [SerializeField] private GameObject waterHorizon;
        
        [Header(H1+"Position Anchoring"+H1)]
        [SerializeField] private bool usePositionAnchor = false;
        [SerializeField] private GameObject positionAnchor;
        [SerializeField] private Vector2 positionStep = Vector2.one * 10;
        
        [Header(H1+"Normals"+H1)]
        [SerializeField] private bool lowPolyNormals = false;
        [SerializeField] private bool calculateNormals = true;
        [SerializeField] private float calculateNormalsFrequency = 1; //in seconds
        private float lastCalculateNormalsTime = 0;
        
        [Header(H1+"Height Map"+H1)]
        private Dictionary<long, float> heightMapDict;
        [SerializeField] private bool useHeightMapArray = true; // This was created for debugging and is almost always faster to be set to true.
        private float[][,] heightMapArray;
        private bool heightMapInitialized = false;

        private List<GameObject> instantiatedList = new List<GameObject>();

        public Vector3 Position => position;

        public void Reset()
        {
            Clear();

            if (Application.isPlaying)
            {
                Start();   
            }
        }

        void Clear()
        {
            foreach (GameObject gameObject in instantiatedList)
            {
                DestroyImmediate(gameObject);
            }
        }
        
        void Start()
        {
            position = transform.position;
            
            SwellManager.Register(this);
            
            if (!meshFilterGrid)
            {
                meshFilterGrid = new GameObject("Mesh Filters");
                instantiatedList.Add(meshFilterGrid);
                meshFilterGrid.transform.parent = transform;
                meshFilterGrid.transform.localPosition = Vector3.zero;
            }
            
            InitializeWaterMesh();

            InitializeWaterHorizon();

            heightMapDict = new Dictionary<long, float>();

            int vertWidth = 11;
            // int heightMapArraySize = 0;
            if (useSwellMesh)
            {
                // heightMapArraySize = gridSize * 10 * (int)gridDensity + 1;
                // heightMapArraySize = gridSize * vertWidth;
                gridSize = (int)(swellMesh.Size / 10);

                heightMapArray = new float[swellMesh.Levels.Length][,];

                for (int levelIndex = 0; levelIndex < swellMesh.Levels.Length; levelIndex++)
                {
                    int levelSize = swellMesh.Levels[levelIndex].VertWidth;
                    heightMapArray[levelIndex] = new float[levelSize,levelSize];
                }
            }
            else
            {
                int heightMapArraySize = meshFilters.Count * vertWidth;
                heightMapArray = new float[1][,];
                heightMapArray[0] = new float[heightMapArraySize,heightMapArraySize];
            }

            if (combineMeshes && !useSwellMesh)
            {
                CombineMeshes();
            }

            if (calculateNormals && lowPolyNormals && meshFilters.Count > 0)
            {
                meshFiltersLowPoly = new List<MeshFilter>(meshFilters);

                Transform parrent = meshFilterGrid.transform;
                for (int i = 0; i < meshFilters.Count; ++i)
                {
                    meshFiltersLowPoly[i] = Instantiate(meshFilters[i], parrent);
                    instantiatedList.Add(meshFiltersLowPoly[i].gameObject);
                    meshFiltersLowPoly[i].gameObject.name = meshFilters[i].name;
                    meshFiltersLowPoly[i].transform.position = meshFilters[i].transform.position;
                    Destroy(meshFiltersLowPoly[i].GetComponent<Collider>());
                    meshFilters[i].GetComponent<MeshRenderer>().enabled = false;

                    if (meshFilters[i] == mergedMeshFilter)
                    {
                        mergedMeshFilter = meshFiltersLowPoly[i];
                    }
                    
                    Destroy(meshFilters[i].gameObject);
                }

                meshFilters = meshFiltersLowPoly;
                if (useSwellMesh)
                {
                    swellMesh = meshFiltersLowPoly[0].GetComponent<SwellMesh>();
                }
            }
        }

        void InitializeWaterMesh()
        {
            if (useSwellMesh)
            {
                if (swellMesh == null)
                {
                    Type[] components = { typeof(SwellMesh) };
                    swellMesh = new GameObject("Swell Mesh", components).GetComponent<SwellMesh>();
                    swellMesh.transform.parent = meshFilterGrid.transform;
                    swellMesh.transform.localPosition = Vector3.zero;
                    swellMesh.Levels = new []
                    {
                        new SwellMesh.MeshLevel(1 , gridSize * tileWidth)
                    };
                }
                    
                if (material)
                {
                    MeshRenderer swellMeshRenderer = swellMesh.GetComponent<MeshRenderer>();
                    swellMeshRenderer.material = material;

                    if (material.shader.name == "Standard")
                    {
                        Material tempTextureMaterial = swellMeshRenderer.material;
                        // Vector2 textureScale = tempTextureMaterial.GetTextureScale("_DetailAlbedoMap") * gridSize;
                        if (mainTextureSize > 0)
                        {
                            tempTextureMaterial.mainTextureScale =
                                Vector2.one * (gridSize * 10 / (float) mainTextureSize);
                        }

                        if (secondaryTextureSize > 0)
                        {
                            tempTextureMaterial.SetTextureScale("_DetailAlbedoMap",
                                Vector2.one * (gridSize * 10 / (float) secondaryTextureSize));
                            tempTextureMaterial.SetTextureScale("_DetailNormalMap",
                                Vector2.one * (gridSize * 10 / (float) secondaryTextureSize));
                        }
                    }
                }

                swellMesh.GenerateMesh();
                centerOffset = tileWidth / 2;

                if (meshFilters != null)
                {
                    meshFilters.Clear();
                }
                else
                {
                    meshFilters = new List<MeshFilter>();
                }
                
                meshFilters.Add(swellMesh.GetComponent<MeshFilter>());
            }
            else
            {
                if (meshFilterPrefab == null)
                {
                    GameObject meshFilterPrefabGameObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    instantiatedList.Add(meshFilterPrefabGameObject);
                    meshFilterPrefabGameObject.name = "Mesh Filter Prefab";
                    meshFilterPrefabGameObject.transform.SetParent(meshFilterGrid.transform);
                    meshFilterPrefabGameObject.transform.localPosition = Vector3.zero;
                    meshFilterPrefabGameObject.SetActive(false);
                    if (material)
                    {
                        meshFilterPrefabGameObject.GetComponent<MeshRenderer>().material = material;
                    }

                    Destroy(meshFilterPrefabGameObject.GetComponent<Collider>());
                    meshFilterPrefab = meshFilterPrefabGameObject.GetComponent<MeshFilter>();
                }

                MeshRenderer prefabMeshRenderer = meshFilterPrefab.GetComponentInChildren<MeshRenderer>();
                prefabMeshRenderer.shadowCastingMode = shadowCastingMode;
                prefabMeshRenderer.receiveShadows = receiveShadows;

                if (meshFilters != null)
                {
                    meshFilters.Clear();
                }

                meshFilters = new List<MeshFilter>();
                overlappingVectors = new List<List<int>>();

                if (meshFilters.Count == 0 && meshFilterPrefab != null)
                {
                    float extent = gridSize / 2f;

                    Transform parent = meshFilterGrid.transform;
                    for (float ix = -extent; ix < extent; ix++)
                    {
                        for (float iy = -extent; iy < extent; iy++)
                        {
                            MeshFilter meshFilter = Instantiate(meshFilterPrefab, parent);
                            instantiatedList.Add(meshFilter.gameObject);
                            meshFilter.gameObject.name = meshFilterPrefab.name + " (Clone)";
                            meshFilter.gameObject.SetActive(true);
                            Destroy(meshFilter.GetComponent<Collider>());
                            float width = tileWidth;
                            centerOffset = width / 2;
                            meshFilter.transform.position = parent.position + new Vector3(
                                ix * width + centerOffset,
                                0,
                                iy * width + centerOffset
                            );
                            meshFilters.Add(meshFilter);
                        }
                    }
                }
            }
        }

        void InitializeWaterHorizon()
        {
            if (useSwellMesh)
            {
                
            }
            else
            {
                String warpName = "WaterHorizon";
                Transform warpParent = transform;
                Vector3 warpScale = new Vector3(.5f, -.02f, .5f);
                Vector3 warpOffset = new Vector3(.5f, 0, .5f);
                Material warpMaterial = null;
                Vector3 scale = Vector3.one * 300;
                if (waterHorizon)
                {
                    StaticMeshWarp staticMeshWarp = waterHorizon.GetComponentInChildren<StaticMeshWarp>();
                    if (staticMeshWarp != null)
                    {
                        warpName = staticMeshWarp.name;
                        warpParent = staticMeshWarp.transform.parent;
                        scale = staticMeshWarp.transform.localScale;
                        warpScale = staticMeshWarp.scale;
                        warpOffset = staticMeshWarp.offset;
                        MeshRenderer staticMeshRenderer = staticMeshWarp.GetComponent<MeshRenderer>();
                        if (staticMeshRenderer)
                        {
                            warpMaterial = staticMeshRenderer.material;
                        }

                        Destroy(staticMeshWarp.gameObject);
                    }
                }

                MeshFilter newMeshFilter = Instantiate(meshFilterPrefab, warpParent);
                MeshRenderer meshRenderer = newMeshFilter.gameObject.GetComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                if (horizonMaterial)
                {
                    meshRenderer.material = horizonMaterial;
                }
                else if (warpMaterial)
                {
                    meshRenderer.material = warpMaterial;
                }

                instantiatedList.Add(newMeshFilter.gameObject);
                newMeshFilter.name = warpName;
                newMeshFilter.transform.position = warpParent.position;
                newMeshFilter.transform.rotation = warpParent.rotation;
                newMeshFilter.transform.localScale = scale;
                Destroy(newMeshFilter.GetComponent<Collider>());
                StaticMeshWarp newStaticMeshWarp = newMeshFilter.gameObject.AddComponent<StaticMeshWarp>();
                newStaticMeshWarp.meshFilter = newMeshFilter;
                newStaticMeshWarp.scale = warpScale;
                newStaticMeshWarp.offset = warpOffset;
                waterHorizon = newStaticMeshWarp.gameObject;
                waterHorizon.SetActive(showWaterHorizon);
            }
        }

        void Update()
        {
            if (usePositionAnchor && positionAnchor)
            {
                Vector3 anchor = positionAnchor.transform.position;

                Vector3 newWaterPosition = new Vector3(
                    anchor.x - anchor.x % positionStep.x,
                    transform.position.y,
                    anchor.z - anchor.z % positionStep.y
                );
                if (transform.position != newWaterPosition)
                {               
                    transform.position = newWaterPosition;
                    meshMoved = true;
                }
                
                if (waterHorizon)
                {
                    waterHorizon.transform.position = new Vector3(
                        anchor.x,
                        waterHorizon.transform.position.y,
                        anchor.z
                    ); 
                }
            }
            position = transform.position;
            
            UpdateWater();
        }

        private void OnDestroy()
        {
            SwellManager.Unregister(this);
        }
        
        void UpdateWater()
        {   
            UpdateHeightMap();
            UpdateMeshes();
            UpdateMeshBoundsAndNormals();
        }

        void CombineMeshes()
        {
            GameObject mergedMeshFilterGameObject;
            if (meshFilterPrefab != null)
            {
                mergedMeshFilter = Instantiate(meshFilterPrefab, meshFilterGrid.transform);
                instantiatedList.Add(mergedMeshFilter.gameObject);
                mergedMeshFilter.gameObject.name = meshFilterPrefab.name + " (Clone)";
                mergedMeshFilter.transform.localPosition = Vector3.zero;
                mergedMeshFilterGameObject = mergedMeshFilter.gameObject;
                mergedMeshFilterGameObject.SetActive(true);
                Destroy(mergedMeshFilter.GetComponent<Collider>());
            }
            else
            {
                mergedMeshFilterGameObject = new GameObject("Merged Water Mesh", typeof(MeshFilter), typeof(MeshRenderer));
                instantiatedList.Add(mergedMeshFilterGameObject);
                mergedMeshFilterGameObject.transform.SetParent(meshFilterGrid.transform);
                mergedMeshFilterGameObject.GetComponent<MeshRenderer>().material = meshFilters[0].gameObject.GetComponent<MeshRenderer>().material;
                mergedMeshFilter = mergedMeshFilterGameObject.GetComponent<MeshFilter>();
            }
            mergedMeshFilter.mesh.Clear();

            CombineInstance[] combine = new CombineInstance[meshFilters.Count];
            int meshIndex = meshFilters.Count -1;
            foreach (MeshFilter meshFilter in meshFilters)
            {
                meshFilter.transform.position -= meshFilterGrid.transform.position;
            }
            while (meshIndex >= 0)
            {
                combine[meshIndex].mesh = meshFilters[meshIndex].mesh;
                combine[meshIndex].transform = meshFilters[meshIndex].transform.localToWorldMatrix;
                Destroy(meshFilters[meshIndex].gameObject);
                meshIndex--;
            }
            meshFilters.Clear();
            meshFilters.Add(mergedMeshFilter);

            mergedMeshFilter.mesh.CombineMeshes(combine);

            Vector3[] verts = mergedMeshFilter.mesh.vertices;

            bool[] vertOverlaps = new bool[verts.Length];
            overlappingVectors = new List<List<int>>();

            for (int vertIndex1 = 0; vertIndex1 < verts.Length; vertIndex1++)
            {
                Vector3 vert1 = verts[vertIndex1];
                List<int> tempOverlappingVectors = new List<int>();
                for (int vertIndex2 = vertIndex1 + 1; vertIndex2 < verts.Length; vertIndex2++)
                {
                    Vector3 vert2 = verts[vertIndex2];
            
                    if (vert1.x == vert2.x && vert1.z == vert2.z)
                    {
                        if (!vertOverlaps[vertIndex1] && !vertOverlaps[vertIndex2])
                        {
                            if (tempOverlappingVectors.Count == 0)
                            {
                                tempOverlappingVectors.Add(vertIndex1);
                            }
                            tempOverlappingVectors.Add(vertIndex2);
                            vertOverlaps[vertIndex2] = true;
                        }
                    }
                }
                
                overlappingVectors.Add(tempOverlappingVectors);
            }

            if (remapUvs)
            {
                float max = gridSize * 10 / 2f;
                Vector2[] uvs = mergedMeshFilter.mesh.uv;
                for (int i = 0; i < uvs.Length; i++)
                {
                    Vector3 vert = verts[i];

                    uvs[i].x = -vert.x / max / 2 + .5f;
                    uvs[i].y = -vert.z / max / 2 + .5f;
                }

                mergedMeshFilter.mesh.uv = uvs;

                Material tempTextureMaterial = mergedMeshFilter.GetComponent<MeshRenderer>().material;
                Vector2 textureScale = tempTextureMaterial.GetTextureScale("_DetailAlbedoMap") * gridSize;
                tempTextureMaterial.SetTextureScale("_DetailAlbedoMap", textureScale);
                tempTextureMaterial.SetTextureScale("_DetailNormalMap", textureScale);
            }
        }

        public float GetWaterHeight(Vector3 position)
        {
            float waterHeight = 0;
            
            if (useSwellMesh)
            {
                int levelIndex = PositionToLevel(position.x, position.z, swellMesh);
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

                    //Get corners 
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
            }
            else
            {
                Vector3 positionOffset = position - this.position;

                float cc = GetHeight(Mathf.CeilToInt(position.x), Mathf.CeilToInt(position.z));
                float ff = GetHeight(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
                float cf = GetHeight(Mathf.CeilToInt(position.x), Mathf.FloorToInt(position.z));
                float fc = GetHeight(Mathf.FloorToInt(position.x), Mathf.CeilToInt(position.z));

                float ratioX = positionOffset.x - Mathf.FloorToInt(positionOffset.x);
                float ratioZ = positionOffset.z - Mathf.FloorToInt(positionOffset.z);

                float x1 = cc + (cf - cc) * (1 - ratioZ);
                float x2 = fc + (ff - fc) * (1 - ratioZ);
                waterHeight = x1 + (x2 - x1) * (1 - ratioX);
            }

            return waterHeight;
        }

        public float GetWaterHeightOptimized(Vector3 position)
        {
            return GetHeight(Mathf.CeilToInt(position.x), Mathf.CeilToInt(position.z));
        }
        
        public float GetHeight(float xPosition, float yPosition, bool calculate=true, int li = -1)
        {
            float height = 0;

            if (useHeightMapArray)
            {
                // Get height from height map array but fall back to calculating height and storing it in dictionary if 
                // it's not in the bounds of the array.
                int xi, yi;
                if (useSwellMesh)
                {
                    // int li = PositionToLevel(xPosition, yPosition, swellMesh);
                    if (li == -1)
                    {
                        (li, xi, yi) = PositionToArrayIndexes(xPosition, yPosition, swellMesh);
                    }
                    else
                    {
                        xi = PositionToIndexX(xPosition, swellMesh.Levels[li]);
                        yi = PositionToIndexY(yPosition, swellMesh.Levels[li]);
                    }
                }
                else
                {
                    li = 0;
                    xi = PositionToIndexX(xPosition);
                    yi = PositionToIndexY(yPosition);    
                }

                // if (heightMapArray == null)
                // {
                //     //This is for use while the Application is not Playing.
                //     height = CalculateHeight(xPosition, yPosition);
                // }
                // else 
                if (xi >= 0 && xi < heightMapArray[li].Length && yi >= 0 && yi < heightMapArray[li].GetLength(1))
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
                
                if (useSwellMesh)
                {
                    (li, xi, yi) = PositionToArrayIndexes(xPosition, yPosition, swellMesh);
                    if (li == -1)
                    {
                        (li, xi, yi) = PositionToArrayIndexes(xPosition, yPosition, swellMesh);
                    }
                }
                else
                {
                    li = 0;
                    xi = PositionToIndexX(xPosition);
                    yi = PositionToIndexY(yPosition);
                }

                // Debug.Log(li + ", " + xi + ", " + yi);
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

        (int, int, int) PositionToArrayIndexes(float positionX, float positionY, SwellMesh swellMesh)
        {
            SwellMesh.MeshLevel level = null;
            for (int li = 0; li < swellMesh.Levels.Length; li++)
            {
                level = swellMesh.Levels[li];
                if (level.InBounds(positionX - position.x, positionY - position.z))
                {
                    int xi = PositionToIndexX(positionX, level);
                    int yi = PositionToIndexY(positionY, level);
                    return (li, xi, yi);
                }
            }

            return (-1, -1, -1);
        }
        
        int PositionToLevel(float positionX, float positionY, SwellMesh swellMesh)
        {
            SwellMesh.MeshLevel level = null;
            for (int li = 0; li < swellMesh.Levels.Length; li++)
            {
                level = swellMesh.Levels[li];
                if (level.InBounds(positionX - position.x, positionY - position.z))
                {
                    return li;
                }
            }
        
            return -1;
        }

        // private const int X = 0;
        // private const int Y = 1;
        // private const int Z = 2;
        //
        // private float Position(int vertex)
        // {
        //     switch (vertex)
        //     {
        //         case X: return position.x;
        //         case Y: return position.y;
        //         case Z: return position.z;
        //     }
        // }
        //
        // int PositionToGrid(int vertex, float positionX, SwellMesh.MeshLevel level)
        // {
        //     
        //     return (int) ((positionX + level.Offset.x - Position(vertex)) / level.Step);
        // }
        
        float PositionToGridX(float positionX, SwellMesh.MeshLevel level)
        {
            return positionX - (positionX + level.Offset.x - position.x) % level.Step;
        }
        
        float PositionToGridY(float positionY, SwellMesh.MeshLevel level)
        {   
            return positionY - (positionY + level.Offset.z - position.z) % level.Step;
        }
        
        int PositionToIndexX(float positionX, SwellMesh.MeshLevel level)
        {
            return (int) ((positionX + level.Offset.x - position.x) / level.Step);
        }
        
        int PositionToIndexY(float positionY, SwellMesh.MeshLevel level)
        {
            return (int) ((positionY + level.Offset.z - position.z) / level.Step);
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
            return (indexX - .5f) * level.Step - level.Offset.x + position.x;
        }
        
        float IndexToPositionY(int indexY, SwellMesh.MeshLevel level)
        {
            return (indexY - .5f) * level.Step - level.Offset.z + position.z;
        }

        int PositionToIndex(float position, float parentPosition)
        {
            // return Mathf.RoundToInt((position + centerOffset * size - parentPosition) * gridDensity + 1);
            return (int) ((position + centerOffset * gridSize - parentPosition) * gridDensity + .5f);
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
            return (index - .5f) / gridDensity - centerOffset * gridSize + parentPosition;
        }
        
        int PositionToGrid(float position)
        {
            return (int)(position * gridDensity + 1);
        }

        float CalculateHeight(float xPosition, float yPosition)
        {
            int x = PositionToGrid(xPosition);
            int y = PositionToGrid(yPosition);

            float totalHeight = 0;

            List<SwellWave> waves = SwellManager.AllWaves;
            for (int i = 0; i < waves.Count; i++)
            {
                totalHeight += waves[i].GetHeight(x, y);
            }

            return totalHeight;
        }

        void UpdateHeightMap()
        {
            if (meshFilters.Count > 0)
            {
                if (meshMoved)
                {
                    meshMoved = false;
                    
                    meshBounds = meshFilters[0].mesh.bounds;
                    meshBounds.center = meshFilters[0].transform.position;
                    for (int i = 0; i < meshFilters.Count; ++i)
                    {
                        Bounds singleMeshBounds = meshFilters[i].mesh.bounds;
                        singleMeshBounds.center = meshFilters[i].transform.position;
                        meshBounds.Encapsulate(singleMeshBounds);
                    }

                    if (lockAlbedoPosition && useSwellMesh)
                    {
                        MeshRenderer swellMeshRenderer = swellMesh.GetComponent<MeshRenderer>();

                        if (swellMeshRenderer.material.shader.name == "Standard")
                        {
                            Vector2 offset = new Vector2(
                                swellMeshRenderer.transform.position.x - meshAlbedoPosition.transform.position.x,
                                swellMeshRenderer.transform.position.z - meshAlbedoPosition.transform.position.z
                            );

                            offset /= swellMesh.Size;
                            swellMeshRenderer.material.mainTextureOffset = offset;
                        }
                    }
                }

                if (useSwellMesh)
                {
                    foreach (Vector3 point in swellMesh.Mesh.vertices)
                    {
                        // Vector3 transformedPoint = swellMesh.transform.TransformPoint(point); // This was causing out of bounds index error on mesh movement
                        Vector3 transformedPoint = point + position;
                        float newHeight = CalculateHeight(transformedPoint.x, transformedPoint.z);
                        SetHeight(transformedPoint.x, transformedPoint.z, newHeight);
                    }
                }
                else
                {
                    for (float x = meshBounds.min.x; x <= meshBounds.max.x; x += 1 / gridDensity)
                    {
                        for (float y = meshBounds.min.z; y <= meshBounds.max.z; y += 1 / gridDensity)
                        {
                            float newHeight = CalculateHeight(x, y);
                            SetHeight(x, y, newHeight);
                        }
                    }
                }
            }
            
            heightMapInitialized = true;
        }

        void UpdateMeshes()
        {
            // Alternative to calling transformPoint on every point. This only works without scale
            // Vector3 startPosition = meshFilters[0].mesh.vertices[0];
            // Vector3 positionChange = startPosition - transform.TransformPoint(startPosition);
            
            for (int j = 0; j < meshFilters.Count; ++j)
            {
                MeshFilter filter = meshFilters[j];
                Mesh mesh = filter.mesh;
                Vector3[] vertices = mesh.vertices;
                
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 v = filter.transform.TransformPoint(vertices[i]);
                    float waveHeight = GetHeight(v.x, v.z, true);

                    vertices[i] = new Vector3(
                        vertices[i].x,
                        waveHeight,
                        vertices[i].z
                    );
                }

                mesh.vertices = vertices;

                if (calculateNormals)
                {
                    if (Time.time - lastCalculateNormalsTime > calculateNormalsFrequency)
                    {
                        if (j == meshFilters.Count - 1)
                        {
                            lastCalculateNormalsTime = Time.time;
                        }

                        if (lowPolyNormals && meshFiltersLowPoly != null && meshFiltersLowPoly.Count > j)
                        {
                            MeshFilter filterLowPoly = meshFiltersLowPoly[j];
                            
                            Mesh meshLowPoly = filterLowPoly.mesh;

                            if (meshLowPoly.vertices.Length != meshLowPoly.triangles.Length)
                            {
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

                                // meshLowPoly.RecalculateBounds();

                                // Begin Function CalculateSurfaceNormal (Input Triangle) Returns Vector
                                //
                                // Set Vector U to (Triangle.p2 minus Triangle.p1)
                                // Set Vector V to (Triangle.p3 minus Triangle.p1)
                                //
                                // Set Normal.x to (multiply U.y by V.z) minus (multiply U.z by V.y)
                                // Set Normal.y to (multiply U.z by V.x) minus (multiply U.x by V.z)
                                // Set Normal.z to (multiply U.x by V.y) minus (multiply U.y by V.x)
                                //
                                // Returning Normal
                            }
                        }
                        else
                        {
                            if (overlappingVectors != null && overlappingVectors.Count > 0)
                            {
                                Vector3[] tempNormals = mergedMeshFilter.mesh.normals;
                                foreach (List<int> overlappingVector in overlappingVectors)
                                {
                                    Vector3 normalsAveraged = new Vector3();
                                    for (int i = 0; i < overlappingVector.Count; i++)
                                    {
                                        int vectorIndex = overlappingVector[i];
                                        normalsAveraged += tempNormals[vectorIndex];
                                    }

                                    normalsAveraged /= overlappingVector.Count;

                                    for (int i = 0; i < overlappingVector.Count; i++)
                                    {
                                        int vectorIndex = overlappingVector[i];
                                        tempNormals[vectorIndex] = normalsAveraged;
                                    }
                                }

                                mergedMeshFilter.mesh.normals = tempNormals;
                            }
                        }
                    }
                }
            }
        }
        
        void UpdateMeshBoundsAndNormals()
        {
            if (mergedMeshFilter && combineMeshes)
            {
                mergedMeshFilter.mesh.RecalculateBounds();
                if (calculateNormals)
                {
                    mergedMeshFilter.mesh.RecalculateNormals();
                    
                    // For all overlapping edges take the normals and average them together
                    Vector3[] tempNormals = mergedMeshFilter.mesh.normals;
                    foreach (List<int> overlappingVector in overlappingVectors)
                    {
                        Vector3 normalsAveraged = new Vector3();
                        for (int i = 0; i < overlappingVector.Count; i++)
                        {
                            int vectorIndex = overlappingVector[i];
                            normalsAveraged += tempNormals[vectorIndex];
                        }
                        normalsAveraged /= overlappingVector.Count;
                
                        for (int i = 0; i < overlappingVector.Count; i++)
                        {
                            int vectorIndex = overlappingVector[i];
                            tempNormals[vectorIndex] = normalsAveraged;
                        }
                    }
                    mergedMeshFilter.mesh.normals = tempNormals;
                }
            }
            else if (meshFiltersLowPoly != null && meshFiltersLowPoly.Count > 0 && lowPolyNormals)
            {
                foreach (MeshFilter meshFilter in meshFiltersLowPoly)
                {
                    meshFilter.mesh.RecalculateBounds();
                    if (calculateNormals)
                    {
                        meshFilter.mesh.RecalculateNormals();
                    }
                }
            }
            else
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    meshFilter.mesh.RecalculateBounds();
                    if (calculateNormals)
                    {
                        meshFilter.mesh.RecalculateNormals();
                    }
                }
            }
        }

        // private bool gizmoFirstSelectedDraw = false;
        // private SwellWave[] gizmoWaves = new SwellWave[] { }; 
        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                float fullSize = tileWidth * gridSize;
                Gizmos.color = new Color(.6f, .6f, .8f);
                Gizmos.DrawWireCube(transform.position, new Vector3(fullSize, 0, fullSize));
                // gizmoFirstSelectedDraw = true;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = new Color(.2f, .2f, .4f);
                float extent = gridSize / 2f;
                float width = tileWidth;
                float offset = width / 2;
                for (float ix = -extent; ix < extent; ix++)
                {
                    for (float iy = -extent; iy < extent; iy++)
                    {
                        Vector3 tilePosition = transform.position + new Vector3(
                            ix * width + offset,
                            0,
                            iy * width + offset
                        );
                        Gizmos.DrawWireCube(tilePosition, new Vector3(width, 0, width));
                    }
                }

                // if (gizmoFirstSelectedDraw)
                // {
                //     gizmoFirstSelectedDraw = false;
                //     gizmoWaves = FindObjectsOfType<SwellWave>();
                // }
                // Gizmos.color = Color.white;
                // int full_extents = (int)(gridSize / 2f * width);
                // float[,] heights = new float[full_extents, full_extents];
                // int xi = 0, yi = 0;
                // for (int positionX = -full_extents; positionX < full_extents; positionX++)
                // {
                //     for (int positionY = -full_extents; positionY < full_extents; positionY++)
                //     {
                //         Vector3 heightPosition = transform.position + new Vector3(positionX, 0, positionY);
                //         heights[positionX,positionY] = CalculateGizmoHeight(heightPosition.x, heightPosition.z, gizmoWaves);
                //         yi++;
                //     }
                //     xi++;
                // }
                //
                // for (xi = 0; xi < heights.Length - 1; xi++)
                // {
                //     for (yi = 0; yi < heights.Length - 1; yi++)
                //     {
                //         // heights[xi, yi]
                //         Vector3 p1 = transform.position + new Vector3()
                //         Gizmos.DrawLine();
                //     }
                // }
                
                float fullSize = tileWidth * gridSize;
                Gizmos.color = new Color(.6f, .6f, .8f);
                Gizmos.DrawWireCube(transform.position, new Vector3(fullSize, 0, fullSize));
            }
        }
        
        // float CalculateGizmoHeight(float xPosition, float yPosition, SwellWave[] waves)
        // {
        //     int x = PositionToGrid(xPosition);
        //     int y = PositionToGrid(yPosition);
        //
        //     float totalHeight = 0;
        //     
        //     foreach (SwellWave wave in waves)
        //     {
        //         totalHeight += wave.GetHeight(x, y, true);
        //     }
        //
        //     return totalHeight;
        // }
    }
}