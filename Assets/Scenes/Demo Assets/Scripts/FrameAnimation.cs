using UnityEngine;

public class FrameAnimation : MonoBehaviour
{
    public Texture2D[] frames;
    public Texture2D[] frameNormals;
    public bool useNormals;
    public int framesPerSecond = 1;
    private int frameIndex = 0;
    private float lastUpdate;
    private Renderer meshRenderer;

    void Start () {
        lastUpdate = Time.time;
        meshRenderer = gameObject.GetComponent<Renderer>();
    }
    
    void Update() {
        float timeSinceUpdate = Time.time - lastUpdate;

        if (timeSinceUpdate > 1f / framesPerSecond)
        {
            lastUpdate = Time.time;
            
            frameIndex++;
            if(frameIndex >= frames.Length){
                frameIndex = 0;
            }
            
            meshRenderer.material.SetTexture("_DetailAlbedoMap", frames[frameIndex]);
            if (useNormals)
            {
                meshRenderer.material.SetTexture("_DetailNormalMap", frameNormals[frameIndex]);
            }
        }
    }
}