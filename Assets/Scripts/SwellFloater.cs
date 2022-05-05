using System.Collections.Generic;
using UnityEngine;
using MyBox;


namespace Swell
{    
    /**
     * @brief Can be attached to any RigidBody to enable float physics in SwellWater.
     *
     * When the SwellFloater.Center point falls below the surface of a SwellFloater.Water, a force is applied to the SwellFloater.Rigidbody at that position with a force relative to the value of the SwellFloater.Buoyancy.
     *
     * # Example
     * See: `Scenes/Swell Floater - Algorithm Demo.unity`
     * ![Gif of Float Algorithm](https://i.imgur.com/71ojK7E.gif)
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_floater.html")]
    public class SwellFloater : MonoBehaviour
    {
        public enum Method { FAST, ACCURATE };

        [field: Separator("Basic Settings"), SerializeField] public float Buoyancy { get; set; } = 5; //!< Upwards force that grows the deeper under water this floater is. 
        [field: SerializeField] public Vector3 Center { get; set; } = Vector3.zero; //!< Position offset to apply depth check and force to rigidbody.

        [Separator("Advanced")] 
        [OverrideLabel(""), SerializeField] private bool showAdvanced; //!< Show advanced settings in inspector.
        [field: SerializeField, ConditionalField(nameof(showAdvanced))] public Method DepthMethod { get; set; } = Method.ACCURATE; //!< Method to use to calculate depth. One is more accurate and the second is faster.
        [field: SerializeField, ConditionalField(nameof(showAdvanced))] public Method FloatMethod { get; set; } = Method.ACCURATE; //!< Method to use to calculate float physics. One is more accurate and the second is faster.
        [field: SerializeField, ConditionalField(nameof(showAdvanced))] public bool Stabilize { get; set; } = false; //!< Experimental feature for rigidbodies with multiple floaters. When enabled this makes adjustments that consider all attached floaters.

        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private new Rigidbody rigidbody;
        [field: SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] public SwellWater Water { get; private set; } //!< The water actively being used to get depth.
        [field: SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] public float Depth { get; private set; } //!< The active depth of this floater under the water.

        private float attachedWeight = 1;
        private static Dictionary<Rigidbody, List<SwellFloater>> attachedFloaters = new ();
        private static int activeFrame = 0;
        private static float gravity;
        private Vector3 position = Vector3.zero;
        private Vector3 Position => position;

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
        } //!< The rigidbody buoyancy force is being applied to. 

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

            if (FloatMethod == Method.FAST && rigidbody)
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
            if (Stabilize)
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

            position = transform.position + transform.rotation * Vector3.Scale(Center, transform.lossyScale);

            Water = SwellManager.GetNearestWater(Position);

            if (DepthMethod == Method.FAST)
            {
                Depth = Position.y - Water.GetWaterHeightOptimized(Position) - Water.Position.y;
            }
            else
            {
                Depth = Position.y - Water.GetWaterHeight(Position) - Water.Position.y;
            }

            if (float.IsNaN(Depth))
            {
                Debug.LogWarning("Swell Warning: depth: " + Depth);
            }

            if (FloatMethod == Method.FAST)
            {
                if (rigidbody)
                {
                    rigidbody.transform.position -= new Vector3(0, Depth, 0);
                }
                else
                {
                    transform.position -= new Vector3(0, Depth, 0);
                }
            }
            else
            {

                if (Depth < 0)
                {
                    Vector3 floatForce = Vector3.up * (Buoyancy * attachedWeight * -Depth * gravity);
                    rigidbody.AddForceAtPosition(floatForce, Position);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Vector3 gizmoPosition =  gizmoPosition = transform.position + transform.rotation * Vector3.Scale(Center, transform.lossyScale);
            
            DrawUnlitSphere(gizmoPosition, .05f);
            Vector3 force = Vector3.one;
            if (FloatMethod == Method.ACCURATE)
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
                
                force = Vector3.up * (Buoyancy / mass);                
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