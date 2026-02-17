using BulletFury;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wayfarer_Games.Common;

namespace Wayfarer_Games.BulletFury.RenderData
{
    [CustomPropertyDrawer(typeof(SharedRenderData), true)]
    public class RenderDataPropertyDrawer : PropertyDrawer
    {
        private static EditorWindow Inspector;
        public static VisualTreeAsset UXML;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var sharedData = property.FindPropertyRelative("sharedData");
            var singleData = property.FindPropertyRelative("singleData");
            
            if (UXML == null)
                UXML = AssetPathUtility.LoadAssetAtKnownRoots<VisualTreeAsset>("Bulletfury/Editor/RenderData/RenderData.uxml");
            
            var root = new VisualElement();
            UXML.CloneTree(root);
            
            var singleDataVE = root.Q<VisualElement>("SingleData");
            var sharedDataVE = root.Q<VisualElement>("SharedData");
            singleDataVE.style.display = sharedData.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
            sharedDataVE.style.display = sharedData.objectReferenceValue == null ? DisplayStyle.None : DisplayStyle.Flex;
            root.Q<ObjectField>("SharedDataSO").objectType = typeof(SharedRenderDataSO);

            void ChangedSharedData(ChangeEvent<Object> e)
            {
                var singlePreview = singleDataVE.Q<Image>("Preview");
                var sharedPreview = sharedDataVE.Q<Image>("Preview");
                if (singlePreview != null)
                    singlePreview.name = e.newValue == null ? "Preview" : "InactivePreview";
                if (sharedPreview != null)
                    sharedPreview.name = e.newValue != null ? "Preview" : "InactivePreview";
                
                singleDataVE.style.display = e.newValue == null ? DisplayStyle.Flex : DisplayStyle.None;
                sharedDataVE.style.display = e.newValue == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            root.Q<ObjectField>("SharedDataSO").RegisterCallback<ChangeEvent<Object>>(ChangedSharedData);
                
            root.Q<Button>("Create").clicked += () =>
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Render Data", "New Render Data", "asset", "Save Render Data");
                var sharedDataSO = ScriptableObject.CreateInstance<SharedRenderDataSO>();
                var currentData = new BulletRenderData
                {
                    Texture = singleData.FindPropertyRelative("Texture").objectReferenceValue as Texture2D,
                    Animated = singleData.FindPropertyRelative("Animated").boolValue,
                    Rows = singleData.FindPropertyRelative("Rows").intValue,
                    Columns = singleData.FindPropertyRelative("Columns").intValue,
                    PerFrameLength = singleData.FindPropertyRelative("PerFrameLength").floatValue,
                    Layer = singleData.FindPropertyRelative("Layer").intValue,
                    Priority = singleData.FindPropertyRelative("Priority").intValue
                };
                sharedDataSO.SetData(currentData);
                AssetDatabase.CreateAsset(sharedDataSO, path);
                sharedData.objectReferenceValue = sharedDataSO;
            };
            
            return root;
        }

    }
}
