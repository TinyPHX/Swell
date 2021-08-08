using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{
    // public GameObject boombox;
    // public GameObject boogieboard;
    public GameObject groundCheck;
    public GameObject headSubmergedCheck;
    public GameObject prefabProjectile;
    public float projectileForce;

    public float rotationForce = 1;
    public float throttleForce = 1;
    public float jumpForce = 1;
    public float airRollForce = 1;

    public float boomBoxAngularRange = 90;

    new Rigidbody rigidbody;
    SwellWater water;

    //Controlls
    float input_rotate = 0;
    float input_throttle_previous = 0;
    float input_throttle = 0;
    float input_verticalRoll = 0;
    float input_horizontalRoll = 0;
    bool input_altRoll = false;
    float input_jump = 0;
    float input_aim = 0;
    float input_horizontalLook = 0;
    float input_verticalLook = 0;
    float input_shoot_alt = 0;
    //float rotateCW = 0;
    //float rotateCCW = 0;

    public bool grounded = false;

    [SerializeField]
    LocalPlayer localPlayer = LocalPlayer.None;
    public enum LocalPlayer { None, P1, P2, P3, P4 };

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        water = FindObjectOfType<SwellWater>();
    }

    private void FixedUpdate()
    {
        UpdateControllsKeyboard();
        UpdateBoomBox();
        AirAdjustment();

        //UpdateControllsController();

        // UpdateGrounded();
        //
        // if (grounded)
        // {
        //     UpdateBoomBox();
        // }
        // else
        // {
        //     AirAdjustment();
        // }
        //
        // CheckForSubmerged();
    }

    void UpdateControllsKeyboard()
    {
        input_rotate = Input.GetAxis("Horizontal_Move");
        input_throttle = Input.GetAxis("Vertical_Move");
        input_altRoll = Input.GetAxis("Jump") > 0;
        input_verticalRoll = input_throttle;
        input_horizontalRoll = input_rotate;
    }

    void UpdateControllsController()
    {
        string playerString = localPlayer.ToString();

        input_rotate = Input.GetAxis(playerString + "_Horizontal_Move");
        input_throttle_previous = input_throttle;
        input_throttle = Input.GetAxis(playerString + "_Shoot");
        input_altRoll = Input.GetAxis(playerString + "_Alt_Roll") > 0;
        input_verticalRoll = Input.GetAxis(playerString + "_Vertical_Move");
        input_horizontalRoll = Input.GetAxis(playerString + "_Horizontal_Move");
        input_horizontalLook = Input.GetAxis(playerString + "_Horizontal_Look");
        input_verticalLook = Input.GetAxis(playerString + "_Vertical_Look");
        input_jump = Input.GetAxis(playerString + "_Jump");
        input_aim = Input.GetAxis(playerString + "_Aim");
        input_shoot_alt = Input.GetAxis(playerString + "_Shoot_Alt");
    }

    void UpdateGrounded()
    {
        // float depth = groundCheck.transform.position.y - water.GetWaterHeight(groundCheck.transform.position);
        // if (depth < 0)
        // {
        //     grounded = true;
        // }
        // else
        // {
        //     grounded = false;
        //
        //     Collider collider = boogieboard.GetComponent<Collider>();
        //     //grounded = Physics.Raycast(new Ray(transform.position - Vector3.down * .3f, transform.position - groundCheck.transform.position), .5f);
        //     grounded = Physics.Raycast(new Ray(transform.position, Vector3.down), collider.bounds.extents.y + .2f);
        //
        // }
    }

    void CheckForSubmerged()
    {
        if (headSubmergedCheck)
        {
            float depth = headSubmergedCheck.transform.position.y -
                          water.GetWaterHeight(headSubmergedCheck.transform.position);
            if (depth < -1)
            {

                //YOU FREAKING LOSE!

                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    void UpdateBoomBox()
    {
        //Debug.Log("input_aim: " + input_aim);
        // if (input_aim > 0)
        // {
        //     if (input_shoot_alt > 0)
        //     {
        //         GameObject tempProjectile = Instantiate<GameObject>(
        //             prefabProjectile, 
        //             boombox.transform.position + new Vector3(
        //                 UnityEngine.Random.Range(-.5f, .5f),
        //                 0,
        //                 UnityEngine.Random.Range(-.5f, .5f)),
        //             boombox.transform.rotation);
        //         Rigidbody tempRigidbody = tempProjectile.GetComponent<Rigidbody>();
        //         tempRigidbody.AddForce(boombox.transform.rotation * new Vector3(0, 1, -1) * projectileForce);
        //         //tempProjectile.velocity = boombox.transform.rotation * Vector3.forward * projectileForce;
        //     }
        //
        //     Vector3 look = new Vector3(input_horizontalLook, 0, input_verticalLook);
        //
        //     if (look.magnitude > .15)
        //     {
        //         // boombox.transform.rotation = Quaternion.AngleAxis(
        //         //     Mathf.Atan2(input_horizontalLook, input_verticalLook) * Mathf.Rad2Deg - 180 + transform.rotation.eulerAngles.y, 
        //         //     Vector3.up);
        //     }
        // }
        // else
        // {
            // Vector3 boomboxRotation = boombox.transform.localEulerAngles;
            //boomboxRotation.y += Time.deltaTime * rotationForce * input_rotate; //Hard mode
            Quaternion rotation = Quaternion.identity;
            Vector3 boomboxRotation = Vector3.zero;
            boomboxRotation.y = -input_rotate * (boomBoxAngularRange / 2);
            rotation.eulerAngles = boomboxRotation;
            // Quaternion globalRotation = rotation * Quaternion.Inverse(transform.rotation);
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
            else
            {
                //boombox.transform.rotation = Quaternion.identity;
            }
        // }
        
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
