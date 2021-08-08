using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class StaticMeshWarp : MonoBehaviour {

    public MeshFilter meshFilter;
    public Vector3 scale;
    public Vector3 offset;
    private Vector3 lastScale;
    private Vector3 lastOffset;

    void Update () {
        if (scale != lastScale || offset != lastOffset)
        {
            lastScale = scale;
            lastOffset = offset;
            
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = true;

            UpdateWarp();
        }
    }

    void UpdateWarp()
    {
        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;

        Vector3 tempPeriod = new Vector3(
            2 * Mathf.PI / 10, 
            1,
            2 * Mathf.PI / 10
            );
        Debug.Log("tempPeriod: " + tempPeriod);
        Vector3 tempWaveOffset = new Vector3(
                tempPeriod.x / 2,
                tempPeriod.y / 2,
                tempPeriod.z / 2
                );

        for (int i = 0; i < vertices.Length; ++i)
        {
            Vector3 v = meshFilter.transform.TransformPoint(vertices[i]);

            float height = Mathf.Sin(vertices[i].x * scale.x * tempPeriod.x + offset.x * Mathf.PI) *
                           Mathf.Sin(vertices[i].z * scale.z * tempPeriod.z + offset.z * Mathf.PI);
            
            height *= scale.y;

            vertices[i] = new Vector3(
                vertices[i].x,
                height,
                vertices[i].z
            );
        }
        
        mesh.vertices = vertices;
        
        mesh.RecalculateBounds();

        var col = GetComponent<MeshCollider>();
        if (col != null)
        {
            var colliMesh = new Mesh();
            colliMesh.vertices = mesh.vertices;
            colliMesh.triangles = mesh.triangles;
            col.sharedMesh = colliMesh;
        }
    }
}
