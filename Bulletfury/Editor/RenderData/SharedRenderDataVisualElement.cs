using System;
using System.Linq;
using BulletFury;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Wayfarer_Games.Common;

namespace Wayfarer_Games.BulletFury.RenderData
{
    [UxmlElement]
    public partial class SharedRenderDataVisualElement : BindableElement
    {
        private static VisualTreeAsset UXML;
        
        public event Action<ChangeEvent<Object>> SharedDataChanged; 
        private SerializedProperty _singleData, _sharedData;
        private Button _createButton;


        public RenderDataVisualElement SingleData { get; private set; }
        public RenderDataVisualElement SharedData { get; private set; }

        public (Image, Image) GetPreview()
        {
            return (SingleData.Preview, SharedData.Preview);
        }
        
        public (VisualElement[], VisualElement[]) GetColliderPreview()
        {
            return (SingleData.ColliderPreview, SharedData.ColliderPreview);
        }

        public SharedRenderDataVisualElement()
        {
            if (UXML == null)
                UXML = AssetPathUtility.LoadAssetAtKnownRoots<VisualTreeAsset>("Bulletfury/Editor/RenderData/RenderData.uxml");

            UXML.CloneTree(this);
            
            SingleData = this.Q<RenderDataVisualElement>("SingleData");
            SharedData = this.Q<RenderDataVisualElement>("SharedData");
            _createButton = this.Q<Button>("Create");
            
            this.Q<ObjectField>("SharedDataSO").objectType = typeof(SharedRenderDataSO);
        }
        
        public void InitWithProperties (SerializedProperty singleData, [CanBeNull] SerializedProperty sharedData)
        {
            _singleData = singleData;
            _sharedData = sharedData;
            
            SingleData.InitWithProperty(singleData);
            if (sharedData != null)
                SharedData.InitWithProperty(sharedData);

            if (sharedData != null)
            {
                #if UNITY_2022_1_OR_NEWER
                SingleData.style.display =
                    sharedData.boxedValue == null ? DisplayStyle.Flex : DisplayStyle.None;
                SharedData.style.display =
                    sharedData.boxedValue == null ? DisplayStyle.None : DisplayStyle.Flex;
                #else
                SingleData.style.display =
                    sharedData.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
                SharedData.style.display =
                    sharedData.objectReferenceValue == null ? DisplayStyle.None : DisplayStyle.Flex;
                #endif
            }
            else
            {
                SingleData.style.display = DisplayStyle.Flex;
                SharedData.style.display = DisplayStyle.None;
            }

            var sharedDataField = this.Q<ObjectField>("SharedDataSO");
            sharedDataField.UnregisterCallback<ChangeEvent<Object>>(OnSharedDataChanged);
            sharedDataField.RegisterCallback<ChangeEvent<Object>>(OnSharedDataChanged);

            _createButton.clicked -= OnCreateClicked;
            _createButton.clicked += OnCreateClicked;
        }

        private void OnCreateClicked()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Render Data", "New Render Data", "asset", "Save Render Data");
            if (string.IsNullOrEmpty(path))
                return;

            var sharedDataSO = ScriptableObject.CreateInstance<SharedRenderDataSO>();
            var currentData = new BulletRenderData
            {
                Texture = _singleData.FindPropertyRelative("Texture").objectReferenceValue as Texture2D,
                Animated = _singleData.FindPropertyRelative("Animated").boolValue,
                Rows = _singleData.FindPropertyRelative("Rows").intValue,
                Columns = _singleData.FindPropertyRelative("Columns").intValue,
                PerFrameLength = _singleData.FindPropertyRelative("PerFrameLength").floatValue,
                Layer = _singleData.FindPropertyRelative("Layer").intValue,
                Priority = _singleData.FindPropertyRelative("Priority").intValue
            };
            sharedDataSO.SetData(currentData);
            AssetDatabase.CreateAsset(sharedDataSO, path);
            if (_sharedData != null)
                _sharedData.objectReferenceValue = sharedDataSO;
        }

        private void OnSharedDataChanged(ChangeEvent<Object> evt)
        {
            if (evt.newValue == evt.previousValue) return;
            
            SharedDataChanged?.Invoke(evt);
            if (evt.newValue == null)
            {
                InitWithProperties(_singleData, null);
            }
            else
            {
                var sharedData = new SerializedObject(evt.newValue).FindProperty("data");
                InitWithProperties(_singleData, sharedData);
            }
        }

    }
}