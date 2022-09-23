using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// This class is based on a script from "Unity/Standard Assets (Mobile)/Scripts/SmoothFollow.cs"
/// </summary>
public class SmoothFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float moveSpeed = 0.02f;
    [SerializeField] float rotateSpeed = 0.06f;
    
    private Vector3 velocity = Vector3.zero;
    
    void FixedUpdate()
    {
        MoveTowardsTarget();
        transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0);
    }

    public void MoveTowardsTarget()
    {
        if (target)
        {
            float cameraDistance = Vector3.Distance(transform.position, target.transform.position);
            transform.position =
                Vector3.MoveTowards(transform.position, target.transform.position, cameraDistance * moveSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target.transform.rotation,
                rotateSpeed * 10);
        }
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
