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
    public Vector3 waveScale = new Vector3(1, 1, 1);
    public Vector3 waveOffset = new Vector3(0, 0, 0);
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
    public bool animate = true;
    public Wave wave1;
    public Wave wave2;
    public bool noise = false;
    public Vector2 noiseSpeed = Vector2.one;
    public Vector3 noiseScale = Vector3.one;
    public bool interpolate;
    public float interpolateSpeed = 1;
    public bool lowPolyNormals = false;
    public bool calculateNormals = false;
    public float calculateNormalsFrequency = 1; //in seconds
    private float lastCalculateNormalsTime = 0;
    public float gridDensity = 1;
    private Dictionary<long, float> heightMapDict = new Dictionary<long, float>();
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
                    float width = meshFilter.mesh.bounds.size.x;
                    float centerOffset = size % 2 == 0 ? width / 2 : width;
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
                anchor.x - anchor.x % 10 - 50,
                transform.position.y,
                anchor.z - anchor.z % 10 - 50
            );
            if (transform.position != newWaterPosition)
            {               
                transform.position = newWaterPosition;
                meshMoved = true;
                UpdateWater();
                UpdateMeshBoundsAndNormals();
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
        
        UpdateWater();
    }
	
	void FixedUpdate ()
    {
        currentPosition = transform.position;

        UpdateWater();
    }

    void UpdateWater()
    {   
        if (animate)
        {
            UpdateWaves();
            UpdateHeightMap();
            UpdateMeshes();
            UpdateMeshBoundsAndNormals();
        }
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
            combine[meshIndex].mesh = meshFilters[meshIndex].sharedMesh;
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
        
        int xi = (int)((xPosition - currentPosition.x) * gridDensity + 1);
        int yi = (int)((yPosition - currentPosition.z) * gridDensity + 1);

        if (xi > 0 && xi < heightMapArray.Length && yi > 0 && yi < heightMapArray[xi].Length)
        {
            height = heightMapArray[xi][yi];
        }
        else
        {
            //Debug.LogWarning(xi + ", " + yi + " out of bounds of heightMapArray.");

            if (calculate)
            {
                var key = PositionToHeightKey(xPosition, yPosition);
                
                if (!heightMapDict.TryGetValue(key, out height))
                {
                    if (calculate)
                    {
                        height = CalculateHeight(xPosition, yPosition);
                    } 
                    else 
                    {
                        height = 0;
                    }
                }
            }
        }
        
        // Coded to use height map for everything! TIS TOO SLOW!
        // if (!heightMapInitialized)
        // {
        //     return 0;
        // }
        //
        // var key = PositionToHeightKey(xPosition, yPosition);

        //return heightMapDict[key];

        // DIS SLOW :(
        // if (!heightMapDict.TryGetValue(key, out float height))
        // {
        //     if (calculate)
        //     {
        //         height = CalculateHeight(xPosition, yPosition);
        //     } 
        //     else 
        //     {
        //         height = 0;
        //     }
        // }
        
        return height;
    }

    public void SetHeight(float xPosition, float yPosition, float height)
    {
        // Coded to use height map for everything! TIS TOO SLOW!
        // var key = PositionToHeightKey(xPosition, yPosition);
        // heightMapDict[key] = height;

        int xi = PositionToIndex(xPosition, currentPosition.x);
        int yi = PositionToIndex(yPosition, currentPosition.z);
        heightMapArray[xi][yi] = height;
    }

    long PositionToHeightKey(float xPosition, float yPosition)
    {
        //Szudzik's pairing function
        //https://stackoverflow.com/questions/919612/mapping-two-integers-to-one-in-a-unique-and-deterministic-way

        int x = PositionToGrid(xPosition);
        int y = PositionToGrid(yPosition);
        
        var A = (ulong)(x >= 0 ? 2 * (long)x : -2 * (long)x - 1);
        var B = (ulong)(y >= 0 ? 2 * (long)y : -2 * (long)y - 1);
        var C = (long)((A >= B ? A * A + A + B : A + B * B) / 2);
        return x < 0 && y < 0 || x >= 0 && y >= 0 ? C : -C - 1;
        
        // return (int)(xPosition * gridDensity + 1) + 1000 * (int)(yPosition * gridDensity + 1);
    }

    int PositionToIndex(float position, float parrentPosition)
    {
        return (int) ((position - parrentPosition) * gridDensity + 1);
    }
    
    int PositionToGrid(float value)
    {
        return (int)(value * gridDensity) + 1;
    }

    float CalculateHeight(float xPosition, float yPosition, float previousHeight = float.NaN)
    {
        int x = PositionToGrid(xPosition);
        int y = PositionToGrid(yPosition);
        
        Vector2 noiseSpeedByTime = Time.time * noiseSpeed;
        Vector3 noiseScaleMultiplier = new Vector3(
            .1f * noiseScale.x,
            .1f * noiseScale.y,
            noiseScale.z 
        );
        float noiseOffset = Mathf.PerlinNoise(
            x * noiseScaleMultiplier.x + noiseSpeedByTime.x, 
            y * noiseScaleMultiplier.y + noiseSpeedByTime.y
        ) * noiseScaleMultiplier.z;

        if (!noise)
        {
            noiseOffset = 0;
        }

        float wave1Height = (
            Mathf.Sin((x * wave1.waveScale.x + wave1.waveOffset.x) * periodX) *
            Mathf.Sin((y * wave1.waveScale.z + wave1.waveOffset.z) * periodY)
        ) * (wave1.enabled ? wave1.waveScale.y : 0) + wave1.waveOffset.y;

        float wave2Height = (
            Mathf.Sin((x * wave2.waveScale.x + wave2.waveOffset.x) * periodX) *
            Mathf.Sin((y * wave2.waveScale.z + wave2.waveOffset.z) * periodY)
        ) * (wave2.enabled ? wave2.waveScale.y : 0) + wave2.waveOffset.y;

        float combinedHeight = wave1Height + wave2Height + noiseOffset;

        float waveHeight = combinedHeight;
        if (interpolate && previousHeight != float.NaN)
        {
            float change = Mathf.Abs(previousHeight - combinedHeight);
            float interpolatedHeight = Mathf.MoveTowards(previousHeight, combinedHeight, Time.deltaTime * change * interpolateSpeed);
            waveHeight = interpolatedHeight;
        }

        return waveHeight;
    }

    void UpdateWaves()
    {
        wave1.waveOffset = new Vector3(Time.time * wave1.waveSpeedMultiplier, 0, Time.time * wave1.waveSpeedMultiplier);
        wave2.waveOffset = new Vector3(Time.time * wave2.waveSpeedMultiplier, 0, Time.time * wave2.waveSpeedMultiplier);
    }

    void UpdateHeightMap()
    {
        if (meshFilters.Count > 0)
        {
            if (meshMoved)
            {
                meshPosition = currentPosition + new Vector3(50, 0, 50);
                meshBounds = meshFilters[0].mesh.bounds;
                for (int i = 0; i < meshFilters.Count; ++i)
                {
                    Bounds singleMeshBounds = meshFilters[i].mesh.bounds;
                    singleMeshBounds.center = meshFilters[i].transform.position;
                    meshBounds.Encapsulate(singleMeshBounds);
                }
            }

            for (float x = meshBounds.min.x + meshPosition.x; x <= meshBounds.max.x + meshPosition.x; x += 1 / gridDensity)
            {
                for (float z = meshBounds.min.z + meshPosition.z; z <= meshBounds.max.z + meshPosition.z; z += 1 / gridDensity)
                {
                    // float currentHeight = GetHeight(x, z, false);
                    // float newHeight = CalculateHeight(x, z, currentHeight);
                    float newHeight = CalculateHeight(x, z);
                    SetHeight(x, z, newHeight);
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
