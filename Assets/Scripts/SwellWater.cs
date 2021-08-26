using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public class Wave
{
    public bool enabled = true;
    public Vector2 waveScale = Vector2.one;
    public Vector2 waveOffset = Vector2.one;
    public float waveHeight;
    public float waveSpeedMultiplier = 1;
}

public class SwellWater : MonoBehaviour
{
    public GameObject meshFilterGrid;
    public bool combineMeshes = false;
    public List<MeshFilter> meshFiltersLowPoly;
    private MeshFilter mergedMeshFilter;
    public MeshFilter meshFilterPrefab;
    public GameObject waterHorizon;
    public bool usePositionAnchor = false;
    public GameObject positionAnchor;
    public Vector2 positionStep = Vector2.one;
    public Wave wave1;
    public Wave wave2;
    public bool noise = false;
    public Vector2 noiseSpeed = Vector2.one;
    public Vector2 noiseScale = Vector3.one;
    public float noiseHeight = 1;
    public bool interpolate;
    public float interpolateSpeed = 1;
    public bool lowPolyNormals = false;
    public bool calculateNormals = false;
    public float calculateNormalsFrequency = 1; //in seconds
    private float lastCalculateNormalsTime = 0;
    public float gridDensity = 1;
    private Dictionary<long, float> heightMapDict = new Dictionary<long, float>();
    public bool useHeightMapArray = true;
    private float[][] heightMapArray;
    private bool heightMapInitialized = false;
    private List<MeshFilter> meshFilters;
    private List<List<int>> overlappingVectors;
    private float periodX;
    private float periodY;
    private Bounds meshBounds;
    private Vector3 meshPosition;
    private bool meshMoved = true;
    private Vector3 currentPosition;

    private int size = 10;
    private int tileWidth = 10;
    private float centerOffset;
    
    void Start ()
    {
        meshFilters = meshFilterGrid.GetComponentsInChildren<MeshFilter>().ToList();

        if (meshFilters.Count == 0 && meshFilterPrefab != null)
        {
            int size = 10;
            float extent = size / 2f;

            Transform parrent = meshFilterGrid.transform;
            for (float ix = -extent; ix < extent; ix++)
            {
                for (float iy = -extent; iy < extent; iy++)
                { 
                    MeshFilter meshFilter = Instantiate(meshFilterPrefab, parrent);
                    meshFilter.gameObject.name = meshFilterPrefab.name;
                    // float width = meshFilter.mesh.bounds.size.x;
                    float width = tileWidth;
                    centerOffset = size % 2 == 0 ? width / 2 : width;
                    meshFilter.transform.position = new Vector3(
                        ix * width + centerOffset,
                        0,
                        iy * width + centerOffset
                    );
                    meshFilters.Add(meshFilter);
                }
            }
        }

        StaticMeshWarp staticMeshWarp = waterHorizon.GetComponentInChildren<StaticMeshWarp>();
        if (staticMeshWarp != null)
        {
            MeshFilter newMeshFilter = Instantiate(meshFilterPrefab, staticMeshWarp.transform.parent);
            newMeshFilter.name = staticMeshWarp.name;
            newMeshFilter.transform.position = staticMeshWarp.transform.position;
            newMeshFilter.transform.rotation = staticMeshWarp.transform.rotation;
            newMeshFilter.transform.localScale = staticMeshWarp.transform.localScale;
            StaticMeshWarp newStaticMeshWarp = newMeshFilter.gameObject.AddComponent<StaticMeshWarp>();
            newStaticMeshWarp.meshFilter = newMeshFilter;
            newStaticMeshWarp.scale = staticMeshWarp.scale;
            newStaticMeshWarp.offset = staticMeshWarp.offset;
            // This is updating the shared materail for some reason :(
            // Material staticMeshWarpMaterial = staticMeshWarp.GetComponent<MeshRenderer>().material;
            // staticMeshWarpMaterial.mainTextureScale = new Vector2(100f, 100f);
                
            Destroy(staticMeshWarp.gameObject);
        }
        
        int vertWidth = 11;
        heightMapArray = new float[meshFilters.Count * vertWidth][];
        for (int i = 0; i < heightMapArray.Length; i++)
        {
            heightMapArray[i] = new float[meshFilters.Count * vertWidth];
        }
        
        periodX = (2 * Mathf.PI / (10 * transform.lossyScale.x));
        periodY = (2 * Mathf.PI / (10 * transform.lossyScale.z));

        if (combineMeshes)
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
                meshFiltersLowPoly[i].gameObject.name = meshFilters[i].name;
                meshFiltersLowPoly[i].transform.position = meshFilters[i].transform.position;
                meshFilters[i].GetComponent<MeshRenderer>().enabled = false;
                Destroy(meshFilters[i].gameObject);
            }

            meshFilters = meshFiltersLowPoly;
        }
    }

    void Update()
    {
        if (usePositionAnchor && positionAnchor)
        {
            Vector3 anchor = positionAnchor.transform.position;

            Vector3 newWaterPosition = new Vector3(
                anchor.x - anchor.x % 10,
                transform.position.y,
                anchor.z - anchor.z % 10
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
        
        currentPosition = transform.position;
        
        UpdateWater();
    }
    
    void UpdateWater()
    {   
        UpdateWaves();
        UpdateHeightMap();
        UpdateMeshes();
        UpdateMeshBoundsAndNormals();
    }

    void CombineMeshes()
    {
        GameObject mergedMeshFilterGameObject;
        if (meshFilterPrefab != null)
        {
            mergedMeshFilter = Instantiate(meshFilterPrefab);
            mergedMeshFilter.gameObject.name = meshFilterPrefab.name;
            mergedMeshFilterGameObject = mergedMeshFilter.gameObject;
            mergedMeshFilterGameObject.transform.SetParent(transform);
            mergedMeshFilterGameObject.SetActive(true);
        }
        else
        {
            mergedMeshFilterGameObject = new GameObject("Merged Water Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mergedMeshFilterGameObject.transform.SetParent(gameObject.transform);
            mergedMeshFilterGameObject.GetComponent<MeshRenderer>().material = meshFilters[0].gameObject.GetComponent<MeshRenderer>().material;
            mergedMeshFilter = mergedMeshFilterGameObject.GetComponent<MeshFilter>();
        }
        mergedMeshFilter.mesh.Clear();

        CombineInstance[] combine = new CombineInstance[meshFilters.Count];
        int meshIndex = meshFilters.Count -1;
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
    }

    public float GetWaterHeight(Vector3 position)
    {
        Vector3 positionOffset = position - currentPosition;

        float cc = GetHeight(Mathf.CeilToInt(position.x), Mathf.CeilToInt(position.z));
        float ff = GetHeight(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
        float cf = GetHeight(Mathf.CeilToInt(position.x), Mathf.FloorToInt(position.z));
        float fc = GetHeight(Mathf.FloorToInt(position.x), Mathf.CeilToInt(position.z));

        float ratioX = positionOffset.x - Mathf.FloorToInt(positionOffset.x);
        float ratioZ = positionOffset.z - Mathf.FloorToInt(positionOffset.z);

        float x1 = cc + (cf - cc) * (1 - ratioZ);
        float x2 = fc + (ff - fc) * (1 - ratioZ);
        float waterHeight = x1 + (x2 - x1) * (1 - ratioX);

        return waterHeight;
    }

    public float GetWaterHeightOptimized(Vector3 position)
    {
        return GetHeight(Mathf.CeilToInt(position.x), Mathf.CeilToInt(position.z));
    }

    public float GetHeight(float xPosition, float yPosition, bool calculate=true)
    {
        float height = 0;

        if (useHeightMapArray)
        {
            // Get height from height map array but fall back to calculating height and storing it in dictionary if 
            // it's not in the bounds of the array. 
            int xi = PositionToIndexX(xPosition);
            int yi = PositionToIndexY(yPosition);

            if (xi >= 0 && xi < heightMapArray.Length && yi >= 0 && yi < heightMapArray[xi].Length)
            {
                height = heightMapArray[xi][yi];
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
            
            int xi = PositionToIndexX(xPosition);
            int yi = PositionToIndexY(yPosition);
            
            heightMapArray[xi][yi] = height;
        }
        else
        {
            var key = PositionToHeightKey(xPosition, yPosition);
            heightMapDict[key] = height;
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

    int PositionToIndexX(float positionX)
    {
        return PositionToIndex(positionX, currentPosition.x);
    }

    int PositionToIndexY(float positionY)
    {
        return PositionToIndex(positionY, currentPosition.z);
    }

    int PositionToIndex(float position, float parrentPosition)
    {
        // return Mathf.RoundToInt((position + centerOffset * size - parrentPosition) * gridDensity + 1);
        return (int) ((position + centerOffset * size - parrentPosition) * gridDensity + .5f);
    }

    float IndexToPositionX(int index)
    {
        return IndexToPosition(index, currentPosition.x);
    }
    
    float IndexToPositionY(int index)
    {
        return IndexToPosition(index, currentPosition.z);
    }
    
    float IndexToPosition(int index, float parrentPosition)
    {
        return (index - .5f) / gridDensity - centerOffset * size + parrentPosition;
    }
    
    int PositionToGrid(float position)
    {
        // return (int)(value * gridDensity) + 1;
        return (int)(position * gridDensity + 1);
    }

    float CalculateHeight(float xPosition, float yPosition)
    {
        int x = PositionToGrid(xPosition);
        int y = PositionToGrid(yPosition);
        
        Vector2 noiseSpeedByTime = Time.time * noiseSpeed;
        Vector3 noiseScaleMultiplier = new Vector3(
            .1f * noiseScale.x,
            .1f * noiseScale.y,
            noiseHeight
        );
        float noiseOffset = Mathf.PerlinNoise(
            x * noiseScaleMultiplier.x + noiseSpeedByTime.x, 
            y * noiseScaleMultiplier.y + noiseSpeedByTime.y
        ) * noiseHeight;

        if (!noise)
        {
            noiseOffset = 0;
        }

        float wave1Height = (
            Mathf.Sin((x * wave1.waveScale.x + wave1.waveOffset.x) * periodX) *
            Mathf.Sin((y * wave1.waveScale.y + wave1.waveOffset.y) * periodY)
        ) * (wave1.enabled ? wave1.waveHeight : 0);

        float wave2Height = (
            Mathf.Sin((x * wave2.waveScale.x + wave2.waveOffset.x) * periodX) *
            Mathf.Sin((y * wave2.waveScale.y + wave2.waveOffset.y) * periodY)
        ) * (wave2.enabled ? wave2.waveHeight : 0);

        float combinedHeight = wave1Height + wave2Height + noiseOffset;

        float waveHeight = combinedHeight;
        if (interpolate)
        {
            float previousHeight = GetHeight(x, y, false);
            float change = Mathf.Abs(previousHeight - combinedHeight);
            float interpolatedHeight = Mathf.MoveTowards(previousHeight, combinedHeight, Time.deltaTime * change * interpolateSpeed);
            waveHeight = interpolatedHeight;
        }

        return waveHeight;
    }

    void UpdateWaves()
    {
        wave1.waveOffset = new Vector2(Time.time * wave1.waveSpeedMultiplier, Time.time * wave1.waveSpeedMultiplier);
        wave2.waveOffset = new Vector2(Time.time * wave2.waveSpeedMultiplier, Time.time * wave2.waveSpeedMultiplier);
    }

    void UpdateHeightMap()
    {
        if (meshFilters.Count > 0)
        {
            if (meshMoved)
            {
                //meshPosition = currentPosition + new Vector3(50, 0, 50);
                meshPosition = currentPosition;
                meshBounds = meshFilters[0].mesh.bounds;
                meshBounds.center = meshFilters[0].transform.position;
                for (int i = 0; i < meshFilters.Count; ++i)
                {
                    Bounds singleMeshBounds = meshFilters[i].mesh.bounds;
                    singleMeshBounds.center = meshFilters[i].transform.position;
                    meshBounds.Encapsulate(singleMeshBounds);
                }
            }

            // Slower than below for some reason. 
            // for (int xi = 0; xi < heightMapArray.Length; xi++)
            // {
            //     for (int yi = 0; yi < heightMapArray[xi].Length; yi++)
            //     {
            //         float x = IndexToPositionX(xi);
            //         float y = IndexToPositionY(yi);
            //         float newHeight = CalculateHeight(x, y);
            //         SetHeight(x, y, newHeight);
            //     }
            // }

            for (float x = meshBounds.min.x; x <= meshBounds.max.x; x += 1 / gridDensity)
            {
                for (float y = meshBounds.min.z; y <= meshBounds.max.z; y += 1 / gridDensity)
                {
                    // float currentHeight = GetHeight(x, z, false);
                    // float newHeight = CalculateHeight(x, z, currentHeight);
                    float newHeight = CalculateHeight(x, y);
                    SetHeight(x, y, newHeight);
                }
            }
        }
        
        heightMapInitialized = true;
    }

    void UpdateMeshes()
    {
        // Alternative to calling transformPoint on ever point. This only works without scale
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
                // Vector3 v = vertices[i] - currentPosition;

                // float waveHeight = CalculateHeight(v.x, v.z, vertices[i].y);
                float waveHeight = GetHeight(v.x, v.z, false);

                vertices[i] = new Vector3(
                    vertices[i].x,
                    waveHeight,
                    vertices[i].z
                );

                // int meshIndexX = Mathf.CeilToInt(filter.transform.position.x - transform.position.x);
                // int meshIndexZ = Mathf.CeilToInt(filter.transform.position.z - transform.position.z);
                //
                // int vertXi = Mathf.CeilToInt(meshIndexX + vertices[i].x);
                // int vertZi = Mathf.CeilToInt(meshIndexZ + vertices[i].z);
                //
                // waterVertices[vertXi][vertZi] = waveHeight;
                
                // int meshIndexX = Mathf.CeilToInt(filter.transform.position.x - transform.position.x);
                // int meshIndexZ = Mathf.CeilToInt(filter.transform.position.z - transform.position.z);
                //
                // int vertXi = meshIndexX + (int)vertices[i].x;
                // int vertZi = meshIndexZ + (int)vertices[i].x;
                //
                // waterVertices[vertXi][vertZi] = waveHeight;
                
                // SetHeight(vertices[i].x, vertices[i].z, waveHeight);
            }

            mesh.vertices = vertices;

            // if (collisionMeshGeneration == algorythmMethod.accurate)
            // {
            //     if (Time.time - lastCollisionMeshGenerationTime > collisionMeshGenerationFrequency)
            //     {
            //         if (j == meshFilters.Count - 1)
            //         {
            //             lastCollisionMeshGenerationTime = Time.time;
            //         }
            //
            //         mesh.RecalculateBounds();
            //
            //         var col = filter.GetComponent<MeshCollider>();
            //         if (col != null)
            //         {
            //             var colliMesh = new Mesh();
            //             colliMesh.vertices = mesh.vertices;
            //             colliMesh.triangles = mesh.triangles;
            //             col.sharedMesh = colliMesh;
            //         }
            //     }
            // }

            if (calculateNormals)
            {
                if (Time.time - lastCalculateNormalsTime > calculateNormalsFrequency)
                {
                    if (j == meshFilters.Count - 1)
                    {
                        lastCalculateNormalsTime = Time.time;
                    }

                    if (lowPolyNormals)
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

                            int newLength = tris1.Length;

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
    
    void OnDrawGizmos()
    {
        // float maxHeight = 4;
        // float padingRatio = 1;
        
        // for (int ix = 0; ix < heightMapArray.Length; ix++)
        // {
        //     for (int iy = 0; iy < heightMapArray[ix].Length; iy++)
        //     {
        //         float height = heightMapArray[ix][iy];
        //         float heightRatio = Mathf.Clamp(height / maxHeight, 0, 1);
        //         Gizmos.color = new Color(
        //             1 - heightRatio,
        //             heightRatio,
        //             0 
        //         );
        //         Gizmos.DrawSphere(new Vector3(ix, height, iy), .1f);
        //     }
        // }
        
        // for (float x = meshBounds.min.x + meshPosition.x; x <= meshBounds.max.x + meshPosition.x; x += 1 / gridDensity)
        // {
        //     for (float z = meshBounds.min.z + meshPosition.z; z <= meshBounds.max.z + meshPosition.z; z += 1 / gridDensity)
        //     {
        //         float height = GetHeight(x, z, false);
        //         float heightRatio = Mathf.Clamp(height / maxHeight, 0, 1);
        //         Gizmos.color = new Color(
        //             1 - heightRatio,
        //             heightRatio,
        //             0
        //         );
        //         Gizmos.DrawSphere(new Vector3(x, height, z), .1f);
        //     }
        // }
    }
}
