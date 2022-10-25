using System.Collections.Generic;
using UnityEngine;

public class FrameAnimation : MonoBehaviour
{
    public string textureString = "_DetailAlbedoMap";
    public Texture2D[] frames;
    public string normalTextureString = "_DetailNormalMap";
    public Texture2D[] frameNormals;
    public int framesPerSecond = 1;
    public List<int> materialIndexes = new List<int>() {0};
    private int frameIndex = 0;
    private int normalFrameIndex = 0;
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

            if (textureString != "" && frames.Length > 0)
            {
                frameIndex++;
                if(frameIndex >= frames.Length){
                    frameIndex = 0;
                }

                for (int i = 0; i < materialIndexes.Count; i++)
                {
                    meshRenderer.materials[materialIndexes[i]].SetTexture(textureString, frames[frameIndex]);   
                }
            }
            
            if (normalTextureString != "" && frameNormals.Length > 0)
            {
                normalFrameIndex++;
                if(normalFrameIndex >= frameNormals.Length){
                    normalFrameIndex = 0;
                }
                
                for (int i = 0; i < materialIndexes.Count; i++)
                {
                    meshRenderer.materials[materialIndexes[i]].SetTexture(normalTextureString, frameNormals[frameIndex]); 
                }
            }
        }
    }
}