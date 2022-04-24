using System.Collections.Generic;
using UnityEngine;
using MyBox;


namespace Swell
{    
    /**
     * @brief MonoBehavior that can be attached to any RigidBody to enable float physics in SwellWater. 
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_floater.html")]
    public class SwellFloater : MonoBehaviour
    {
        private enum Method { FAST, ACCURATE };

        [Separator("Basic Settings")] 
        [SerializeField] private float buoyancy = 1; //!< TODO
        [SerializeField] private Vector3 center = Vector3.zero; //!< TODO

        [Separator("Advanced")] 
        [OverrideLabel("")]
        [SerializeField] private bool showAdvanced; //!< TODO
        [SerializeField, ConditionalField(nameof(showAdvanced))] private Method depthMethod = Method.ACCURATE; //!< TODO
        [SerializeField, ConditionalField(nameof(showAdvanced))] private Method floatMethod = Method.ACCURATE; //!< TODO
        [SerializeField, ConditionalField(nameof(showAdvanced))] private bool stabilize = false; //!< TODO
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private new Rigidbody rigidbody; //!< TODO
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private SwellWater water; //!< TODO
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private float depth; //!< TODO
        
        private float attachedWeight = 1;
        private static Dictionary<Rigidbody, List<SwellFloater>> attachedFloaters = new ();

        private static int activeFrame = 0;
        private static float gravity;

        private Vector3 position = Vector3.zero;
        public Vector3 Position => position;

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

        void Start()
        {
            gravity = Physics.gravity.magnitude;
            UpdateRigidBody();
            RigidbodyChanged(null, rigidbody);
            this.Register();

            if (floatMethod == Method.FAST && rigidbody)
            {
                rigidbody.useGravity = false;
            }
        }

        private void OnDestroy()
        {
            this.UnRegister();
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
            if (stabilize)
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
                foreach (Rigidbody rigidbodyWithFloater in attachedFloaters.Keys)
                {
                    float averageHeight = 0;
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    foreach (SwellFloater floater in attachedFloaters[rigidbodyWithFloater])
                    {
                        float height = floater.Position.y;
                        averageHeight += height;
                        if (min > height)
                        {
                            min = height;
                        }

                        if (max < height)
                        {
                            max = height;
                        }
                    }

                    averageHeight /= attachedFloaters[rigidbodyWithFloater].Count;

                    if (attachedFloaters[rigidbodyWithFloater].Count > 1)
                    {
                        foreach (SwellFloater floater in attachedFloaters[rigidbodyWithFloater])
                        {
                            float height = floater.Position.y;
                            floater.attachedWeight = (height - min) / (max - min);
                        }
                    }

                }
            }

            activeFrame = Time.frameCount;
        }

        public void FixedUpdate()
        {
            OncePerRigidBodyUpdate();

            position = transform.position + transform.rotation * Vector3.Scale(center, transform.lossyScale);

            water = SwellManager.GetNearestWater(Position);

            if (depthMethod == Method.FAST)
            {
                depth = Position.y - water.GetWaterHeightOptimized(Position) - water.Position.y;
            }
            else
            {
                depth = Position.y - water.GetWaterHeight(Position) - water.Position.y;
            }

            if (float.IsNaN(depth))
            {
                Debug.LogWarning("Swell Warning: depth: " + depth);
            }

            if (floatMethod == Method.FAST)
            {
                if (rigidbody)
                {
                    rigidbody.transform.position -= new Vector3(0, depth, 0);
                }
                else
                {
                    transform.position -= new Vector3(0, depth, 0);
                }
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
            Vector3 gizmoPosition =  gizmoPosition = transform.position + transform.rotation * Vector3.Scale(center, transform.lossyScale);
            
            DrawUnlitSphere(gizmoPosition, .05f);
            Vector3 force = Vector3.one;
            if (floatMethod == Method.ACCURATE)
            {
                float mass = 1;
                if (!rigidbody)
                {
                    UpdateRigidBody();
                }

                if (rigidbody)
                {
                    mass = rigidbody.mass;
                }
                
                force = Vector3.up * (buoyancy / mass);                
            }
            Gizmos.DrawRay(gizmoPosition, force);
            DrawUnlitSphere(gizmoPosition + force, .05f);
        }

        void DrawUnlitSphere(Vector3 origin, float radius)
        {
            for (float adjustedRadius = radius; adjustedRadius > 0; adjustedRadius -= radius / 100)
            {
                Gizmos.DrawWireSphere(origin, adjustedRadius);
            }
        }
    }
}