using System.IO;
using UnityEditor;
using UnityEngine;

namespace TP
{
    public class ReadmeUtil
    {
        
        public static GUISkin GetSkin(string path, string fileName)
        {
            GUISkin guiSkin = default;
            
            string file = fileName + ".guiskin";
            string filePath = Path.Combine(path, file).Replace("\\Editor\\..", "");
            if (File.Exists(Path.GetFullPath(filePath)))
            {
                guiSkin = (GUISkin)AssetDatabase.LoadAssetAtPath(filePath, typeof(GUISkin));
            }
            else
            {
                Debug.LogWarning("GetSkin file not found.");
            }

            return guiSkin;
        }
    }
}