using System.Collections.Generic;
using UnityEngine;

public enum algorythmMethod { fast, accurate };

public class SwellFloater : MonoBehaviour {

    [SerializeField] private float buoyancy = 1;

    [SerializeField] private Rigidbody rigidbody;
    [SerializeField] private algorythmMethod depthMethod = algorythmMethod.accurate;
    [SerializeField] private algorythmMethod floatMethod = algorythmMethod.accurate;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private bool stabalize = false;
    
    private SwellWater water;
    private float attachedWeight = 1;
    private float depth;

    private static Dictionary<Rigidbody, List<SwellFloater>> attachedFloaters = new Dictionary<Rigidbody, List<SwellFloater>>();
    private static int activeFrame = 0;
    private static float gravity;

    public Vector3 Position
    {
        get
        {
            return transform.position + transform.rotation * Vector3.Scale(center, transform.lossyScale);
        }
    }

    public Rigidbody Rigidbody
    {
        get => rigidbody;
        set
        {
            if (rigidbody != value)
            {
                RigidbodyChanged(rigidbody, value);
                rigidbody = value;
            }
        }
    }

    public List<SwellFloater> AttachedFloaters => attachedFloaters[rigidbody];

    public void Reset()
    {
        UpdateRigidBody();
    }

	void Start ()
    {
        gravity = Physics.gravity.magnitude;
        UpdateRigidBody();
        RigidbodyChanged(null, rigidbody);
        SwellManager.Register(this);
    }

    void UpdateRigidBody()
    {
        if (rigidbody == null)
        {
            Rigidbody = GetComponent<Rigidbody>();
        }
        if (rigidbody == null)
        {
            Rigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    void RigidbodyChanged(Rigidbody previousRigidbody, Rigidbody newRigidbody)
    {
        if (stabalize)
        {
            if (previousRigidbody != null)
            {
                attachedFloaters[previousRigidbody].Remove(this);
            }

            if (!attachedFloaters.ContainsKey(newRigidbody))
            {
                attachedFloaters.Add(newRigidbody, new List<SwellFloater>());
            }

            attachedFloaters[newRigidbody].Add(this);
        }
    }

    void OncePerRigidBodyUpdate()
    {
        if (activeFrame != Time.frameCount)
        {
            foreach (Rigidbody rigidbody in attachedFloaters.Keys)
            {
                float average = 0;
                float min = float.MaxValue;
                float max = float.MinValue;
                foreach (SwellFloater floater in attachedFloaters[rigidbody])
                {
                    float height = floater.Position.y;
                    average += height;
                    if (min > height)
                    {
                        min = height;
                    }

                    if (max < height)
                    {
                        max = height;
                    }
                }

                average /= attachedFloaters[rigidbody].Count;

                if (attachedFloaters[rigidbody].Count > 1)
                {
                    foreach (SwellFloater floater in attachedFloaters[rigidbody])
                    {
                        float height = floater.Position.y;
                        floater.attachedWeight = (height - min) / (max - min);
                    }
                }

            }
        }
        activeFrame = Time.frameCount;
    }
	
	// Update is called once per frame
	void FixedUpdate()
    {
        OncePerRigidBodyUpdate();
        
        water = SwellManager.GetNearestWater(Position);

        if (depthMethod == algorythmMethod.fast)
        {
            depth = Position.y - water.GetWaterHeightOptimized(Position);
        }
        else
        {
            depth = Position.y - water.GetWaterHeight(Position);
        }

        if (depth == float.NaN)
        {
            Debug.LogWarning("Swell Warning: depth: " + depth);
        }

        if (floatMethod == algorythmMethod.fast)
        {
            transform.position -= new Vector3(0, depth, 0);
        }
        else
        {
            if (depth < 0)
            {
                Vector3 floatForce = Vector3.up * (buoyancy * attachedWeight * -depth * gravity);
                rigidbody.AddForceAtPosition(floatForce, Position);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        float fakeDepth = 1;
        float fakeAttachedWeight = 1;
        DrawUnlitSphere(Position, .05f);
        Vector3 force = Vector3.up * (buoyancy / rigidbody.mass);
        Gizmos.DrawRay(Position, force);
        DrawUnlitSphere(Position + force, .05f);
    }

    void DrawUnlitSphere(Vector3 position, float radius)
    {
        for (float adjustedRadius = radius; adjustedRadius > 0; adjustedRadius -= radius / 100)
        {
            Gizmos.DrawWireSphere(position, adjustedRadius);
        }
    }
}
