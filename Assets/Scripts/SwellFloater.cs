using System.Collections.Generic;
using UnityEngine;
using MyBox;


namespace Swell
{
    /**
     * @brief MonoBehavior that can be attached to any RigidBody to enable float physics in SwellWater. 
     * @author mkatic
     * 
     * Test image
     * 
     * ![Gif of Float Algorythm](../../images/float_algorythm_demo.gif)
     * 
     * doxygen test documentation
     *
     * @param test this is the only parameter of this test function. It does nothing!
     *
     * # Supported elements
     *
     * These elements have been tested with the custom CSS.
     *
     * ## Tables
     *
     * The table content is scrollable if the table gets too wide.
     * 
     * | first_column | second_column | third_column | fourth_column | fifth_column | sixth_column | seventh_column | eighth_column | ninth_column |
     * |--------------|---------------|--------------|---------------|--------------|--------------|----------------|---------------|--------------|
     * | 1            | 2             | 3            | 4             | 5            | 6            | 7              | 8             | 9            |
     *
     *
     * ## Lists
     *
     * - element 1
     * - element 2
     *
     * 1. element 1
     *    ```
     *    code in lists
     *    ```
     * 2. element 2
     *
     * ## Quotes
     *
     * > Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt 
     * > ut labore et dolore magna aliqua. Vitae proin sagittis nisl rhoncus mattis rhoncus urna neque viverra. 
     * > Velit sed ullamcorper morbi tincidunt ornare. 
     * > 
     * > Lorem ipsum dolor sit amet consectetur adipiscing elit duis.
     * *- jothepro*
     *
     * ## Code block
     *
     * ```cpp
     * auto x = "code within md fences (```)";
     * ```
     *
     * @code{.cpp}
     * // code within @code block
     * while(true) {
     *    auto example = std::make_shared<Example>(5);
     *    example->test("test");
     * }
     * 
     * @endcode
     *
     *     // code within indented code block
     *     auto test = std::shared_ptr<Example(5);
     *
     *
     * Inline `code` elements in a text. *Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.* This also works within multiline text and does not break the `layout`.
     *
     *
     * ## Special hints
     *
     * @warning this is a warning only for demonstration purposes
     *
     * @note this is a note to show that notes work. They can also include `code`:
     * @code{.c}
     * void this_looks_awesome();
     * @endcode
     *
     * @bug example bug
     *
     * @deprecated None of this will be deprecated, because it's beautiful!
     *
     * @invariant This is an invariant
     *
     * @pre This is a precondition
     *
     * @todo This theme is never finished!
     *
     * @remark This is awesome!
     */
    [HelpURL("https://tinyphx.github.io/Swell/html/class_swell_1_1_swell_floater.html")]
    public class SwellFloater : MonoBehaviour
    {
        public enum Method
        {
            FAST,
            ACCURATE
        };

        [Separator("Basic Settings")] 
        [SerializeField] private float buoyancy = 1;
        [SerializeField] private Vector3 center = Vector3.zero;

        [Separator("Advanced")] 
        [OverrideLabel("")]
        [SerializeField] private bool showAdvanced;
        [SerializeField, ConditionalField(nameof(showAdvanced))] private Method depthMethod = Method.ACCURATE;
        [SerializeField, ConditionalField(nameof(showAdvanced))] private Method floatMethod = Method.ACCURATE;
        [SerializeField, ConditionalField(nameof(showAdvanced))] private bool stabilize = false;
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private new Rigidbody rigidbody;
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private SwellWater water;
        [SerializeField, ReadOnly, ConditionalField(nameof(showAdvanced))] private float depth;
        
        private float attachedWeight = 1;
        private static Dictionary<Rigidbody, List<SwellFloater>> attachedFloaters =
            new Dictionary<Rigidbody, List<SwellFloater>>();

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
            // SwellManager.Register(this);
            this.Register();

            if (floatMethod == Method.FAST && rigidbody)
            {
                rigidbody.useGravity = false;
            }
        }

        private void OnDestroy()
        {
            // SwellManager.Unregister(this);
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