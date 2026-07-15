#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NeoMantra2026.Scripts
{
    [CustomPropertyDrawer(typeof(LayerFieldAttribute))]
    public class LayerFieldAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
                property.intValue = EditorGUI.LayerField(position, label, property.intValue);
            else
                EditorGUI.PropertyField(position, property, label);
        }
    }
}
#endif
