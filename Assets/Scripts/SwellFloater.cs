using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum algorythmMethod { fast, accurate };

public class SwellFloater : MonoBehaviour {

    public float boyancy = 1;

    MeshFilter meshFilter;
    SwellWater water;
    public Rigidbody rigidbody;
    public algorythmMethod depthMethod;
    public algorythmMethod floatMethod;

	// Use this for initialization
	void Start () {
        meshFilter = GetComponent<MeshFilter>();
        water = FindObjectOfType<SwellWater>();
        if (rigidbody == null)
        {
            rigidbody = GetComponent<Rigidbody>();
        }
    }
	
	// Update is called once per frame
	void FixedUpdate() {
        float depth;

        if (depthMethod == algorythmMethod.fast)
        {
            depth = transform.position.y - water.GetWaterHeightOptimized(transform.position);
        }
        else
        {
            depth = transform.position.y - water.GetWaterHeight(transform.position);
        }

        if (depth == float.NaN)
        {
            Debug.Log("depth: " + depth);
        }

        if (floatMethod == algorythmMethod.fast)
        {
            transform.position -= new Vector3(0, depth, 0);
        }
        else
        {
            if (depth < -.2f)
            {
                depth = -.2f;
            }

            if (depth < 0)
            {
                Vector3 floatForce = Vector3.up * (boyancy * -depth * rigidbody.mass);
                rigidbody.AddForceAtPosition(floatForce, transform.position);
            }
        }
    }
}
