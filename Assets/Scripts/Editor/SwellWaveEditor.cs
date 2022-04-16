using System;
using UnityEditor;
using UnityEngine;

namespace Swell.Editors
{
    /**
     * @brief `Editor` for SwellMesh component 
     */
    [CustomEditor(typeof(SwellWave)), CanEditMultipleObjects]
    public class SwellWaveEditor : Editor
    {
        // List<SerializedProperty> basicProperties;
        // List<SerializedProperty> advancedProperties;
        SerializedProperty myProperty;

        private void OnEnable()
        {
            myProperty = serializedObject.FindProperty("customWave");
            // serializedObject.FindProperty()
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            // DrawDefaultInspector();
            
            // SwellWave[] swellWaveTargets = Array.ConvertAll(targets, item => (SwellWave) item);
            
            // EditorGUILayout.PropertyField(m_BoolProp, new GUIContent("Wave Enabled 2"));
            // EditorGUILayout.PropertyField(myProperty, new GUIContent("Wave Enabled 2"), GUILayout.Height(100));
            

            // foreach (SerializedProperty serializedProperty in serializedObject.GetIterator())
            // {
            //     Debug.Log("TEST 1 "+ serializedProperty.type );
            //     Debug.Log("TEST 2 "+ nameof(AnimationCurve));
            // }
            
            ///////////////////////////////////////////////////

            // serializedObject.Update(); //maybe
            //
            // SerializedProperty serializedProperty = serializedObject.GetIterator();
            // serializedProperty.Next(true);
            // do
            // {
            //     Debug.Log("TEST 1 " + serializedProperty.type);
            //     Debug.Log("TEST 2 " + typeof(AnimationCurve).ToString());
            //
            //     if (serializedProperty.type == "AnimationCurve")
            //     {
            //         EditorGUILayout.PropertyField(serializedProperty,  GUILayout.Height(100));
            //     }
            //     else
            //     {
            //         EditorGUILayout.PropertyField(serializedProperty);
            //     }
            // } while (serializedProperty.Next(false));
            //
            // serializedObject.ApplyModifiedProperties();
            
            ///////////////////////////////////////////////////

            // foreach (SwellWave swellWave in swellWaveTargets)
            // {
            //
            //     swellWave.WaveEnabled = GUILayout.Toggle(swellWave.WaveEnabled, "Wave Enabled");
            // }
            //
            // if (GUILayout.Button("Generate"))
            // {
            //     foreach (SwellWave swellWave in swellWaveTargets)
            //     {
            //         
            //     }
            // }
        }
    }
}