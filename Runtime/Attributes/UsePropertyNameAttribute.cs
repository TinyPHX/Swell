using UnityEngine;
using UnityEditor;

//This is a fix for property serialization in Unity versions older that 2021.1.

namespace Swell
{
    /// <summary>
    /// Use this attribute in combination with a [SerializeField] attribute on top of a property to display the property name. Example:
    /// [field: SerializeField, UsePropertyName]
    /// public int number { get; private set; }
    /// </summary>
    public class UsePropertyNameAttribute : PropertyAttribute
    {
        public bool readOnly; //Have to provide this readonly option because this is not compatible with the "Readonly" mybox attribute. 
        
        public UsePropertyNameAttribute(bool readOnly = false)
        {
            this.readOnly = readOnly;
        }
    }
    
    #if UNITY_2021_1_OR_NEWER
        //Do nothing. In newer version of unity properties are serialized properly.
    #else 
        [CustomPropertyDrawer(typeof(UsePropertyNameAttribute))]
        public class UsePropertyNameAttributeDrawer : PropertyDrawer
        {
            
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                if (!(attribute is UsePropertyNameAttribute usePropertyNameAttribute)) return;
                
                if (property.name.EndsWith("k__BackingField"))
                {
                    FixLabel(label);
                }
                
                if (usePropertyNameAttribute.readOnly) { GUI.enabled = false; }
                EditorGUI.PropertyField(position, property, label, true);
                if (usePropertyNameAttribute.readOnly) { GUI.enabled = true; }
            }

            private static void FixLabel(GUIContent label)
            {
                var text = label.text;
                var firstLetter = char.ToUpper(text[1]);
                label.text = firstLetter + text.Substring(2, text.Length - 19);
            }
        }
    #endif
}
