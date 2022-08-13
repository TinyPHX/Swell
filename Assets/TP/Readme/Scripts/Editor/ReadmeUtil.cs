using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TP
{
    public static class ReadmeUtil
    {
        #region Gui Skins
        
        public static readonly string SKIN_STYLE = "Style";
        public static readonly string SKIN_SOURCE = "Source";

        public static GUISkin GetSkin(string fileName, ScriptableObject script)
        {
            string GetSkinsPath()
            {
                MonoScript monoScript = MonoScript.FromScriptableObject(script);
                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) ?? "";
                path = Path.Combine(path, "..");
                path = Path.Combine(path, "Skins");
                return path;
            }
            
            string path = GetSkinsPath();
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
        
        #endregion
        
        public static void FocusEditorWindow(string windowTitle)
        {
            EditorWindow inspectorWindow = GetEditorWindow(windowTitle);
            if (inspectorWindow != default(EditorWindow))
            {
                inspectorWindow.Focus();
            }

            EditorWindow GetEditorWindow(string windowTitle)
            {
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                EditorWindow editorWindow =
                    allWindows.SingleOrDefault(window => window.titleContent.text == windowTitle);

                return editorWindow;
            }
        }
        
        public static string GetFixedLengthId(string id, int length = 7)
        {
            string fixedLengthId = id;
            bool isNegative = id[0] == '-';
            string prepend = "";

            if (isNegative)
            {
                prepend = "-";
                fixedLengthId = id.Substring(1, id.Length - 1);
            }

            while (fixedLengthId.Length + prepend.Length < length)
            {
                fixedLengthId = "0" + fixedLengthId;
            }

            fixedLengthId = prepend + fixedLengthId;

            return fixedLengthId;
        }
    }
}