using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float rotationForce = 1;
    public float throttleForce = 1;
    public float airRollForce = 1;
    public float angularRange = 120;
    new Rigidbody rigidbody;
    float input_rotate = 0;
    float input_throttle = 0;
    float input_verticalRoll = 0;
    float input_horizontalRoll = 0;
    bool input_altRoll = false;

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        UpdateControllsKeyboard();
        UpdateRotation();
        AirAdjustment();
    }

    void UpdateControllsKeyboard()
    {
        input_rotate = Input.GetAxis("Horizontal");
        input_throttle = Input.GetAxis("Vertical");
        input_altRoll = Input.GetAxis("Jump") > 0;
        input_verticalRoll = input_throttle;
        input_horizontalRoll = input_rotate;
    }

    void UpdateRotation()
    {
        Quaternion rotation = Quaternion.identity;
        Vector3 boomboxRotation = Vector3.zero;
        boomboxRotation.y = -input_rotate * (angularRange / 2);
        rotation.eulerAngles = boomboxRotation;
        Quaternion globalRotation = rotation * transform.rotation;
    
        if (input_throttle > 0)
        {
            if (input_altRoll)
            {
                input_throttle *= -.2f;
            }
    
            Vector3 rotatedtThrottleForce = globalRotation * Vector3.forward * throttleForce * input_throttle;
            Vector3 throttleOrigin = new Vector3(transform.position.x, transform.position.y, transform.position.z);
    
            rigidbody.AddForceAtPosition(rotatedtThrottleForce, throttleOrigin);
        }
        
        Vector3 veloocity = Quaternion.Inverse(transform.rotation) * rigidbody.velocity;
        veloocity.x *= .9f;
        rigidbody.velocity = transform.rotation * veloocity;

    }

    void AirAdjustment()
    {
        rigidbody.AddRelativeTorque(Time.deltaTime * input_verticalRoll * airRollForce, 0, 0);

        if (!input_altRoll)
        {
            rigidbody.AddRelativeTorque(0, Time.deltaTime * input_horizontalRoll * airRollForce, 0);
        }
        else
        {
            rigidbody.AddRelativeTorque(0, 0, Time.deltaTime * -input_horizontalRoll * airRollForce);
        }
    }
}
