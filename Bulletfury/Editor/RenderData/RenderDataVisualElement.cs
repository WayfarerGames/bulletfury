using System.Linq;
using BulletFury;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wayfarer_Games.Common;

namespace Wayfarer_Games.BulletFury.RenderData
{
    [UxmlElement]
    public partial class RenderDataVisualElement : BindableElement
    {
        private static VisualTreeAsset UXML;

        public Image Preview { get; private set; }

        public VisualElement[] ColliderPreview { get; private set; }
        
        
        private SerializedProperty _property;
        private Image _preview;
        private VisualElement _animatedProperties;
        private VisualElement[] _colliderPreview;
        private Label _frameCount;
        private int _currentFrame;
        private ObjectField _textureField;
        private PropertyField _animatedField;
        private PropertyField _rowsField;
        private PropertyField _columnsField;
        private Button _nextFrameButton;
        private Button _previousFrameButton;

        public RenderDataVisualElement()
        {
            if (UXML == null)
                UXML = AssetPathUtility.LoadAssetAtKnownRoots<VisualTreeAsset>("Bulletfury/Editor/RenderData/RenderDataTextureSettings.uxml");

            UXML.CloneTree(this);

            Preview = this.Q<Image>("Preview");

            ColliderPreview = Preview.Children().ToArray();
        }

        public void InitWithProperty(SerializedProperty property)
        {
            if (property == null) return;
            
            _property = property;

            this.BindProperty(property);
            
            _textureField ??= this.Q<ObjectField>("Texture");
            _animatedField ??= this.Q<PropertyField>("Animated");
            _rowsField ??= this.Q<PropertyField>("Rows");
            _columnsField ??= this.Q<PropertyField>("Columns");
            _nextFrameButton ??= this.Q<Button>("NextFrame");
            _previousFrameButton ??= this.Q<Button>("PreviousFrame");

            _textureField.UnregisterCallback<ChangeEvent<Object>>(SetTexture);
            _textureField.RegisterCallback<ChangeEvent<Object>>(SetTexture);
            _animatedProperties = this.Q<VisualElement>("AnimatedProperties");
            if (property.FindPropertyRelative("Animated").boolValue)
                _animatedProperties.RemoveFromClassList("hidden");
            else 
                _animatedProperties.AddToClassList("hidden");
            
            _animatedField.UnregisterCallback<ChangeEvent<bool>>(ChangeAnimated);
            _animatedField.RegisterCallback<ChangeEvent<bool>>(ChangeAnimated);
            _rowsField.UnregisterCallback<ChangeEvent<int>>(ChangeRows);
            _rowsField.RegisterCallback<ChangeEvent<int>>(ChangeRows);
            _columnsField.UnregisterCallback<ChangeEvent<int>>(ChangeColumns);
            _columnsField.RegisterCallback<ChangeEvent<int>>(ChangeColumns);

            _frameCount = this.Q<Label>("FrameCount");
            
            _currentFrame = 0;
            UpdatePreviewForCurrentFrame();

            _nextFrameButton.clicked -= NextFrame;
            _nextFrameButton.clicked += NextFrame;

            _previousFrameButton.clicked -= PreviousFrame;
            _previousFrameButton.clicked += PreviousFrame;

        }

        private void NextFrame()
        {
            var totalFrames = GetTotalFrames();
            _currentFrame++;
            if (_currentFrame >= totalFrames)
                _currentFrame = 0;

            UpdatePreviewForCurrentFrame();
        }

        private void PreviousFrame()
        {
            var totalFrames = GetTotalFrames();
            _currentFrame--;
            if (_currentFrame < 0)
                _currentFrame = totalFrames - 1;

            UpdatePreviewForCurrentFrame();
        }

        private int GetTotalFrames()
        {
            if (_property == null)
                return 1;

            var rows = Mathf.Max(1, _property.FindPropertyRelative("Rows").intValue);
            var columns = Mathf.Max(1, _property.FindPropertyRelative("Columns").intValue);
            return Mathf.Max(1, rows * columns);
        }

        private void UpdatePreviewForCurrentFrame()
        {
            if (_property == null)
                return;

            var rows = Mathf.Max(1, _property.FindPropertyRelative("Rows").intValue);
            var columns = Mathf.Max(1, _property.FindPropertyRelative("Columns").intValue);
            var totalFrames = Mathf.Max(1, rows * columns);
            _currentFrame = Mathf.Clamp(_currentFrame, 0, totalFrames - 1);

            int currentRow = _currentFrame / columns;
            int currentColumn = _currentFrame % columns;

            var uv = Preview.uv;
            uv.x = (float)currentColumn / columns;
            uv.y = 1f - (float)(currentRow + 1) / rows;

            Preview.uv = uv;
            Preview.MarkDirtyRepaint();
            _frameCount.text = $"Frame {_currentFrame} / {totalFrames}";
        }
        
        private void ChangeColumns(ChangeEvent<int> evt)
        {
            var uv = Preview.uv;
            uv.height = 1f / evt.newValue;
            Preview.uv = uv;
            Preview.MarkDirtyRepaint();
            _frameCount.text = $"Frame {_currentFrame} / " +
                               _property.FindPropertyRelative("Rows").intValue *
                               _property.FindPropertyRelative("Columns").intValue;
        }

        private void ChangeRows(ChangeEvent<int> evt)
        {
            var uv = Preview.uv;
            uv.width = 1f / evt.newValue;
            Preview.uv = uv;
            Preview.MarkDirtyRepaint();
            _frameCount.text = $"Frame {_currentFrame} / " +
                               _property.FindPropertyRelative("Rows").intValue *
                               _property.FindPropertyRelative("Columns").intValue;
        }

        private void ChangeAnimated(ChangeEvent<bool> evt)
        {
            _animatedProperties.SetEnabled(evt.newValue);
            if (evt.newValue)
            {
                var uv = Preview.uv;
                uv.width = 1f / _property.FindPropertyRelative("Rows").intValue;
                uv.height = 1f/ _property.FindPropertyRelative("Columns").intValue;
                Preview.uv = uv;
                Preview.MarkDirtyRepaint();
                _animatedProperties.RemoveFromClassList("hidden");
            }
            else
            {
                _animatedProperties.AddToClassList("hidden");
                var uv = Preview.uv;
                uv.width = 1f;
                uv.height = 1f;
                Preview.uv = uv;
                Preview.MarkDirtyRepaint();
            }
            
        }
        
        private void SetTexture(ChangeEvent<Object> evt)
        {
            Preview.image = evt.newValue as Texture2D;
        }

    }
}