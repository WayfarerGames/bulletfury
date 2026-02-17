using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Wayfarer_Games.Common.FloatOrRandom.Editor
{
    [CustomPropertyDrawer(typeof(Common.FloatOrRandom.IntOrRandom))]
    public class IntOrRandomDrawer : PropertyDrawer
    {
        private static VisualTreeAsset UXML;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (UXML == null)
                UXML = AssetPathUtility.LoadAssetAtKnownRoots<VisualTreeAsset>("Common/FloatOrRandom/Editor/PropertyDrawer.uxml");
            
            var root = new VisualElement();
            UXML.CloneTree(root);

            root.Q<Label>().text = property.displayName;

            var to = root.Q<Label>("To");
            var min = root.Q<IntegerField>("Min");
            var max = root.Q<IntegerField>("Max");
            var random = root.Q<Toggle>("IsRandom");
            random.tooltip = "If true, the value will be random between min and max. If false, the value will be min.";
            
            min.bindingPath = property.propertyPath + ".minValue";
            min.Bind(property.serializedObject);
            max.bindingPath = property.propertyPath + ".maxValue";
            max.Bind(property.serializedObject);
            random.bindingPath = property.propertyPath + ".isRandom";
            random.Bind(property.serializedObject);
            
            random.RegisterValueChangedCallback((evt) =>
            {
                to.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                max.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return root;
        }

    }
}