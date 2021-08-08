using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// This class is based on a script from "Unity/Standard Assets (Mobile)/Scripts/SmoothFollow.cs"
/// </summary>
public class SmoothFollow : MonoBehaviour
{
    [SerializeField]
    Transform target;
    [SerializeField]
    float smoothTime = 0.1f;
    //[SerializeField]
    //float smoothRotateTime = 2f;


    private Vector3 velocity = Vector3.zero;
    
    void Start()
    {

    }

    void FixedUpdate()
    {
        MoveTowardsTarget();

 
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0);
    }

    public void MoveTowardsTarget()
    {
        //Vector3 newPosition;
        //newPosition.x = Mathf.SmoothDamp(transform.position.x, target.position.x, ref velocity.x, smoothTime);
        //newPosition.y = Mathf.SmoothDamp(transform.position.y, target.position.y, ref velocity.y, smoothTime);
        //newPosition.z = Mathf.SmoothDamp(transform.position.z, target.position.z, ref velocity.z, smoothTime);

        //transform.position = newPosition;

        float cameraDistance = Vector3.Distance(transform.position, target.transform.position);

        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, cameraDistance * smoothTime);
        
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target.transform.rotation, cameraDistance * smoothTime * 10);
    }

    public void MoveToTarget()
    {
        transform.position = target.position;
    }

    public void MoveTargetToHere()
    {
        target.position = transform.position;
    }
}

#if UNITY_EDITOR
[ExecuteInEditMode]
[CustomEditor(typeof(SmoothFollow))]
public class SmoothFollow2DEditor : Editor
{
    SmoothFollow smoothFollow;

    void OnEnable()
    {
        smoothFollow = FindObjectOfType<SmoothFollow>();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Move to target"))
        {
            smoothFollow.MoveToTarget();
        }

        if (GUILayout.Button("Move target here"))
        {
            smoothFollow.MoveTargetToHere();
        }
    }
}
#endif
