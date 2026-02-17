using BulletFury;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wayfarer_Games.BulletFury.RenderData
{
    [CustomPropertyDrawer(typeof(SharedRenderDataSO))]
    public class SharedRenderDataSOPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            if (property.objectReferenceValue == null) return root;
            var serializedObject = new SerializedObject(property.objectReferenceValue);
            Debug.Log(serializedObject.FindProperty("data").displayName);
            var prop = new PropertyField(serializedObject.FindProperty("data"));
            prop.Bind(serializedObject);
            root.Add(prop);
            return root;
        }
    }
}