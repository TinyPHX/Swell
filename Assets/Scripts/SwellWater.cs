using System;
using System.Collections;
using System.Collections.Generic;
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

public class SwellWater : MonoBehaviour {

    public List<MeshFilter> meshFilters = new List<MeshFilter>();
    public bool combineMeshes = false;
    public List<MeshFilter> meshFiltersLowPoly = new List<MeshFilter>();
    public MeshFilter mergedMeshFilter;

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
    
    // public algorythmMethod collisionMeshGeneration = algorythmMethod.fast;
    // public float collisionMeshGenerationFrequency = 1; //in seconds
    // float lastCollisionMeshGenerationTime = 0;

    public bool lowPolyNormals = false;
    public bool calculateNormals = false;
    public float calculateNormalsFrequency = 1; //in seconds
    float lastCalculateNormalsTime = 0;
    float[][] waterVertices;
    float meshSize = 10;

    float radius = 1.5f;

    private List<List<int>> overlappingVectors;

    // Use this for initialization
    void Start ()
    {
        int vertWidth = 11;
        waterVertices = new float[meshFilters.Count * vertWidth][];
        for (int i = 0; i < waterVertices.Length; i++)
        {
            waterVertices[i] = new float[meshFilters.Count * vertWidth];
        }

        if (combineMeshes)
        {
            CombineMeshes(); //Needs work. Messes up the texture and animations

            // for (int j = 0; j < meshFilters.Count; ++j)
            // {
            //     if (calculateNormals)
            //     {
            //         MeshFilter filter = meshFilters[j];
            //         Mesh mesh = filter.mesh;
            //
            //         Vector3[] oldVerts = mesh.vertices;
            //         int[] triangles = mesh.triangles;
            //         Vector3[] vertices = new Vector3[triangles.Length];
            //         for (int i = 0; i < triangles.Length; i++)
            //         {
            //             vertices[i] = oldVerts[triangles[i]];
            //             triangles[i] = i;
            //         }
            //         mesh.vertices = vertices;
            //         mesh.triangles = triangles;
            //     }
            // }
        }

        if (calculateNormals && lowPolyNormals)
        {
            meshFiltersLowPoly = new List<MeshFilter>(meshFilters);
            
            for (int i = 0; i < meshFilters.Count; ++i)
            {
                meshFiltersLowPoly[i] = Instantiate(meshFilters[i], transform);
                meshFiltersLowPoly[i].transform.position = meshFilters[i].transform.position;
                meshFilters[i].GetComponent<MeshRenderer>().enabled = false;
                // meshFilters[i].gameObject.SetActive(false);
                Destroy(meshFilters[i].gameObject);
                // meshFilters[i] = meshFiltersLowPoly[i];
            }

            meshFilters = meshFiltersLowPoly;
            // meshFiltersLowPoly = meshFilters;
        }
    }

    void Update()
    {
        if (positionAnchor)
        {
            //Cant do this because of wave interpolation. Need to move edge meshes around instead
            Vector3 newPosition = positionAnchor.transform.position;
            newPosition.y = transform.position.y;
            newPosition.x = newPosition.x - newPosition.x % 10 - 50;
            newPosition.z = newPosition.z - newPosition.z % 10 - 50;
            transform.position = newPosition;
        }
        
        Vector3 lockedPosition = transform.position;

        if (lockedPosition.x % positionStep.x != 0 || lockedPosition.z % positionStep.y != 0)
        {
            Debug.Log("Position updated");
        
            lockedPosition.x = lockedPosition.x - lockedPosition.x % positionStep.x;
            lockedPosition.z = lockedPosition.z - lockedPosition.z % positionStep.y;

            transform.position = lockedPosition;
            
            if (animate)
            {
                bool savedInterpolate = interpolate;
                interpolate = false;
                AddWaves();
                interpolate = savedInterpolate;
            }
        }
    }
	
	// Update is called once per frame
	void FixedUpdate ()
    {
        if (animate)
        {
            AddWaves();
        }
    }

    void CombineMeshes()
    {
        GameObject mergedMeshFilterGameObject;
        if (mergedMeshFilter)
        {
            mergedMeshFilterGameObject = mergedMeshFilter.gameObject;
            mergedMeshFilterGameObject.SetActive(true);
        }
        else
        {
            mergedMeshFilterGameObject = new GameObject("Merged Water Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mergedMeshFilterGameObject.transform.SetParent(gameObject.transform);
            mergedMeshFilterGameObject.GetComponent<MeshRenderer>().material = meshFilters[0].gameObject.GetComponent<MeshRenderer>().material;
        }
        mergedMeshFilter = mergedMeshFilterGameObject.GetComponent<MeshFilter>();
        mergedMeshFilter.mesh.Clear();

        CombineInstance[] combine = new CombineInstance[meshFilters.Count];
        int meshIndex = meshFilters.Count -1;
        while (meshIndex >= 0)
        {
            combine[meshIndex].mesh = meshFilters[meshIndex].sharedMesh;
            combine[meshIndex].transform = meshFilters[meshIndex].transform.localToWorldMatrix;
            //meshFilters[meshIndex].gameObject.SetActive(false);
            Destroy(meshFilters[meshIndex].gameObject);
            meshIndex--;
        }
        meshFilters.Clear();
        meshFilters.Add(mergedMeshFilter);

        mergedMeshFilter.mesh.CombineMeshes(combine, true, true, false);
        // mergedMeshFilter.mesh.CombineMeshes(combine, false, false);
        // triMeshMerged.mesh.CombineMeshes(combine, true, true, false);
        // triMeshSplit.mesh.CombineMeshes(combine, true, true, false);

        Vector3[] verts = mergedMeshFilter.mesh.vertices;
        int[] tris = mergedMeshFilter.mesh.triangles;
        Vector2[] uvs = mergedMeshFilter.mesh.uv;
        Vector3[] normals = mergedMeshFilter.mesh.normals;

        bool[] vertOverlaps = new bool[verts.Length];
        
        // AutoWeld(mergedMeshFilter.mesh, .1f);
        
        // int[,] overlappingVectors = new int[verts.Length, 4];
        overlappingVectors = new List<List<int>>();

        for (int vertIndex1 = 0; vertIndex1 < verts.Length; vertIndex1++)
        {
            Vector3 vert1 = verts[vertIndex1];
            List<int> tempOverlappingVectors = new List<int>();
            // for (int vertIndex2 = 0; vertIndex2 < verts.Length; vertIndex2++)
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
                        
                        for (int triIndex = 0; triIndex < tris.Length; triIndex++)
                        {
                            int triVertIndex = tris[triIndex];

                            if (triVertIndex == vertIndex2)
                            {
                                // tris[triIndex] = vertIndex1;
                            }
                        }

                        // verts[vertIndex2] = verts[vertIndex1];
                    }
                }
            }
            
            overlappingVectors.Add(tempOverlappingVectors);
        }

        // triMeshMerged.mesh.triangles = tris;
        // triMeshSplit.mesh.triangles = mergedMeshFilter.mesh.triangles;

        // mergedMeshFilter.mesh.vertices = verts;
        
        // mergedMeshFilter.mesh.triangles = triMeshMerged.mesh.triangles;
        
        mergedMeshFilter.mesh.RecalculateBounds();
        mergedMeshFilter.mesh.RecalculateNormals();
        
        Vector3[] tempNormals = mergedMeshFilter.mesh.normals;
        foreach (List<int> overlappingVector in overlappingVectors)
        {
            // Vector3[] overlappingNormals = new Vector3[overlappingVector.Count];
            Vector3 normalsAveraged = new Vector3();
            for (int i = 0; i < overlappingVector.Count; i++)
            {
                int vectorIndex = overlappingVector[i];
                // overlappingNormals[i] = mergedMeshFilter.mesh.normals[vectorIndex];
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

        // mergedMeshFilter.mesh.triangles = triMeshSplit.mesh.triangles;

        // Vector3[] oldVerts = mergedMeshFilter.mesh.vertices;
        // int[] oldTris = mergedMeshFilter.mesh.triangles;
        // Vector2[] oldUvs = mergedMeshFilter.mesh.uv;
        //
        // int[] indexMap = new int[oldVerts.Length];
        //
        // int[] newVerts = new int[oldVerts.Length];
        // int[] newTris = new int[oldTris.Length];
        // // Vector2[] uvs = mergedMeshFilter.mesh.uv;

        // AutoWeld(mergedMeshFilter.mesh, .1f);

        // int newVertIndex = 0;
        // for (int vertIndex1 = 0; vertIndex1 < oldVerts.Length; vertIndex1++)
        // {
        //     Vector3 oldVert1 = oldVerts[vertIndex1];
        //     for (int vertIndex2 = vertIndex1 + 1; vertIndex2 < oldVerts.Length; vertIndex2++)
        //         // for (int vertIndex2 = vertIndex1 + 1; vertIndex2 < verts.Length; vertIndex2++)
        //     {
        //         Vector3 oldVert2 = oldVerts[vertIndex2];
        //
        //         if (oldVert1.x == oldVert2.x &&
        //             //vert1.y == vert2.y &&
        //             oldVert1.z == oldVert2.z)
        //         {
        //             if (!vertUsed[vertIndex1] && !vertUsed[vertIndex2])
        //             {
        //                 newVerts[newVertIndex] = oldVerts[vertIndex1];
        //                 indexMap[vertIndex1] = newVertIndex;
        //                 indexMap[vertIndex2] = newVertIndex;
        //                 // uvs[vertIndex2] = uvs[vertIndex1];
        //
        //                 newVertIndex++;
        //
        //                 vertUsed[vertIndex2] = true;
        //             }
        //         }
        //     }
        //     
        //     for (int triIndex = 0; triIndex < oldTris.Length; triIndex++)
        //     {
        //         int triOldVertIndex = oldTris[triIndex];
        //         
        //         newTris[triIndex] = indexMap[triOldVertIndex];
        //
        //         // if (triOldVertIndex == vertIndex2)
        //         // {
        //         //     tris[triIndex] = vertIndex1;
        //         // }
        //     }
        // }
        //
        // mergedMeshFilter.mesh.triangles = newTris;
        // mergedMeshFilter.mesh.vertices = newVerts;
        // // mergedMeshFilter.mesh.uv = uvs;
        // mergedMeshFilter.mesh.RecalculateBounds();
        // mergedMeshFilter.mesh.RecalculateNormals();
    }
    
    private void AutoWeld(Mesh mesh, float threshold) {
        Vector3[] verts = mesh.vertices;
         
        // Build new vertex buffer and remove "duplicate" verticies
        // that are within the given threshold.
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
         
        int k = 0;
         
        foreach (Vector3 vert in verts) {
            // Has vertex already been added to newVerts list?
            foreach (Vector3 newVert in newVerts)
                if (Vector3.Distance(newVert, vert) <= threshold)
                    goto skipToNext;
             
            // Accept new vertex!
            newVerts.Add(vert);
            newUVs.Add(mesh.uv[k]);
             
            skipToNext:;
            ++k;
        }
         
        // Rebuild triangles using new verticies
        int[] tris = mesh.triangles;
        for (int i = 0; i < tris.Length; ++i) {
            // Find new vertex point from buffer
            for (int j = 0; j < newVerts.Count; ++j) {
                if (Vector3.Distance(newVerts[j], verts[ tris[i] ]) <= threshold) {
                    tris[i] = j;
                    break;
                }
            }
        }
         
        // Update mesh!
        mesh.Clear();
        mesh.vertices = newVerts.ToArray();
        mesh.triangles = tris;
        mesh.uv = newUVs.ToArray();
        mesh.RecalculateBounds();
    }

    public float GetWaterHeight(Vector3 position)
    {
        Vector3 positionOffset = position - transform.position;

        float cc = waterVertices[Mathf.CeilToInt(positionOffset.x)][Mathf.CeilToInt(positionOffset.z)];
        float ff = waterVertices[Mathf.FloorToInt(positionOffset.x)][Mathf.FloorToInt(positionOffset.z)];
        float cf = waterVertices[Mathf.CeilToInt(positionOffset.x)][Mathf.FloorToInt(positionOffset.z)];
        float fc = waterVertices[Mathf.FloorToInt(positionOffset.x)][Mathf.CeilToInt(positionOffset.z)];

        float ratioX = positionOffset.x - Mathf.FloorToInt(positionOffset.x);
        float ratioZ = positionOffset.z - Mathf.FloorToInt(positionOffset.z);

        float x1 = cc + (cf - cc) * (1 - ratioZ);
        float x2 = fc + (ff - fc) * (1 - ratioZ);
        float waterHeight = x1 + (x2 - x1) * (1 - ratioX);

        return waterHeight;
    }

    public float GetWaterHeightOptimized(Vector3 position)
    {
        Vector3 positionOffset = position - transform.position;

        float waterHeight = waterVertices[Mathf.CeilToInt(positionOffset.x)][Mathf.CeilToInt(positionOffset.z)];
        
        return waterHeight;
    }

    void AddWaves()
    {
        for (int j = 0; j < meshFilters.Count; ++j)
        {
            MeshFilter filter = meshFilters[j];
            Mesh mesh = filter.mesh;
            Vector3[] vertices = mesh.vertices;

            wave1.waveOffset = new Vector3(Time.time * wave1.waveSpeedMultiplier, 0, Time.time * wave1.waveSpeedMultiplier);
            wave2.waveOffset = new Vector3(Time.time * wave2.waveSpeedMultiplier, 0, Time.time * wave2.waveSpeedMultiplier);
            float periodX = (2 * Mathf.PI / (10 * transform.lossyScale.x));
            float periodY = (2 * Mathf.PI / (10 * transform.lossyScale.z));

            if (!wave1.enabled)
            {
                wave1.waveScale.y = 0;
            }

            if (!wave2.enabled)
            {
                wave2.waveScale.y = 0;
            }

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = filter.transform.TransformPoint(vertices[i]);

                Vector2 noiseSpeedByTime = Time.time * noiseSpeed;
                Vector3 noiseScaleMultiplier = new Vector3(
                    .1f * noiseScale.x,
                    .1f * noiseScale.y,
                    noiseScale.z 
                    );
                float noiseOffset = Mathf.PerlinNoise(
                    v.x * noiseScaleMultiplier.x + noiseSpeedByTime.x, 
                    v.z * noiseScaleMultiplier.y + noiseSpeedByTime.y
                ) * noiseScaleMultiplier.z;

                if (!noise)
                {
                    noiseOffset = 0;
                }

                float wave1Height = (
                    Mathf.Sin((v.x * wave1.waveScale.x + wave1.waveOffset.x) * periodX) *
                    Mathf.Sin((v.z * wave1.waveScale.z + wave1.waveOffset.z) * periodY)
                    ) * wave1.waveScale.y + wave1.waveOffset.y;

                float wave2Height = (
                    Mathf.Sin((v.x * wave2.waveScale.x + wave2.waveOffset.x) * periodX) *
                    Mathf.Sin((v.z * wave2.waveScale.z + wave2.waveOffset.z) * periodY)
                    ) * wave2.waveScale.y + wave2.waveOffset.y;

                float combinedHeight = wave1Height + wave2Height + noiseOffset;


                float waveHeight = combinedHeight;
                if (interpolate)
                {
                    float change = Mathf.Abs(vertices[i].y - combinedHeight);
                    float interpolatedHeight = Mathf.MoveTowards(vertices[i].y, combinedHeight, Time.deltaTime * change * interpolateSpeed);
                    waveHeight = interpolatedHeight;
                }

                vertices[i] = new Vector3(
                    vertices[i].x,
                    waveHeight,
                    vertices[i].z
                );

                int meshIndexX = Mathf.CeilToInt(filter.transform.position.x - transform.position.x);
                int meshIndexZ = Mathf.CeilToInt(filter.transform.position.z - transform.position.z);

                int vertXi = Mathf.CeilToInt(meshIndexX + vertices[i].x);
                int vertZi = Mathf.CeilToInt(meshIndexZ + vertices[i].z);

                waterVertices[vertXi][vertZi] = waveHeight;
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
                            // meshLowPoly.uv = uv2;
                            meshLowPoly.tangents = tang2;
                            meshLowPoly.normals = normal2;
                            meshLowPoly.triangles = tris2;

                            meshLowPoly.RecalculateBounds();

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
                        
                        meshLowPoly.RecalculateNormals();
                    }
                    else
                    {
                        // mergedMeshFilter.mesh.triangles = triMeshMerged.mesh.triangles;
                        mergedMeshFilter.mesh.RecalculateBounds();
                        mergedMeshFilter.mesh.RecalculateNormals();
                        // mergedMeshFilter.mesh.triangles = triMeshSplit.mesh.triangles;
                        
                        Vector3[] tempNormals = mergedMeshFilter.mesh.normals;
                        foreach (List<int> overlappingVector in overlappingVectors)
                        {
                            // Vector3[] overlappingNormals = new Vector3[overlappingVector.Count];
                            Vector3 normalsAveraged = new Vector3();
                            for (int i = 0; i < overlappingVector.Count; i++)
                            {
                                int vectorIndex = overlappingVector[i];
                                // overlappingNormals[i] = mergedMeshFilter.mesh.normals[vectorIndex];
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

        //transform.gameObject.active = true;
    }

    void ModifyMesh(Vector3 displacement, Vector3 center)
    {
        foreach (var filter in meshFilters)
        {
            Mesh mesh = filter.mesh;
            Vector3[] vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 v = filter.transform.TransformPoint(vertices[i]);
                vertices[i] = vertices[i] + displacement * Gaussian(v, center, radius);
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();

            // var col = filter.GetComponent<MeshCollider>();
            // if (col != null)
            // {
            //     var colliMesh = new Mesh();
            //     colliMesh.vertices = mesh.vertices;
            //     colliMesh.triangles = mesh.triangles;
            //     col.sharedMesh = colliMesh;
            // }
        }
    }

    static float Gaussian(Vector3 pos, Vector3 mean, float dev)
    {
        float x = pos.x - mean.x;
        float y = pos.y - mean.y;
        float z = pos.z - mean.z;
        float n = 1.0f / (2.0f * Mathf.PI * dev * dev);
        return n * Mathf.Pow(2.718281828f, -(x * x + y * y + z * z) / (2.0f * dev * dev));
    }
}
