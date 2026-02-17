using BulletFury;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wayfarer_Games.Common;

namespace Wayfarer_Games.BulletFury.RenderData
{
    [CustomPropertyDrawer(typeof(BulletRenderData), true)]
    public class RenderDataChildPropertyDrawer : PropertyDrawer
    {
        public static VisualTreeAsset UXML;
        
        private SerializedProperty _property;
        private Image _preview;
        private VisualElement _animatedProperties;
        private VisualElement[] _colliderPreview;
        private Label _frameCount;
        private int _currentFrame;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            _property = property;
            if (UXML == null)
                UXML = AssetPathUtility.LoadAssetAtKnownRoots<VisualTreeAsset>("Bulletfury/Editor/RenderData/RenderDataTextureSettings.uxml");
            
            var root = new VisualElement();
            UXML.CloneTree(root);
            
            _preview = root.Q<Image>("Preview");
            root.Q<ObjectField>("Texture").RegisterCallback<ChangeEvent<Object>>(SetTexture);
            _animatedProperties = root.Q<VisualElement>("AnimatedProperties");
            if (property.FindPropertyRelative("Animated").boolValue)
                _animatedProperties.RemoveFromClassList("hidden");
            else 
                _animatedProperties.AddToClassList("hidden");
            
            root.Q<PropertyField>("Animated").RegisterCallback<ChangeEvent<bool>>(ChangeAnimated);
            root.Q<PropertyField>("Rows").RegisterCallback<ChangeEvent<int>>(ChangeRows);
            root.Q<PropertyField>("Columns").RegisterCallback<ChangeEvent<int>>(ChangeColumns);

            _frameCount = root.Q<Label>("FrameCount");
            
            _currentFrame = 0;
            var uv = _preview.uv;
            uv.x = 0;
            uv.y = 0;
            _preview.uv = uv;
            _preview.MarkDirtyRepaint();
            _frameCount.text = "0 " +
                               property.FindPropertyRelative("Rows").intValue *
                               property.FindPropertyRelative("Columns").intValue;
            
            root.Q<Button>("NextFrame").clicked += () =>
            {
                _currentFrame++;
                if (_currentFrame >= property.FindPropertyRelative("Rows").intValue *
                    property.FindPropertyRelative("Columns").intValue)
                    _currentFrame = 0;
                
                var uv = _preview.uv;
                uv.x += uv.width;
                if (uv.x >= 1)
                {
                    uv.x = 0;
                    uv.y -= uv.height;
                    
                    if (uv.y < 0)
                        uv.y = 1-uv.height;
                }
                _preview.uv = uv;
                _preview.MarkDirtyRepaint();
                _frameCount.text = $"Frame {_currentFrame} / " +
                                   property.FindPropertyRelative("Rows").intValue *
                                   property.FindPropertyRelative("Columns").intValue;
            };

            root.Q<Button>("PreviousFrame").clicked += () =>
            {
                _currentFrame--;
                if (_currentFrame < 0)
                    _currentFrame = property.FindPropertyRelative("Rows").intValue *
                                    property.FindPropertyRelative("Columns")
                                        .intValue;
                
                var uv = _preview.uv;
                uv.x -= uv.width;
                if (uv.x < 0)
                {
                    uv.x = 1-uv.width;
                    uv.y += uv.height;
                    
                    if (uv.y > 1)
                        uv.y = 0;
                }
                _preview.uv = uv;
                _preview.MarkDirtyRepaint();
                _frameCount.text = $"Frame {_currentFrame} / " +
                                   property.FindPropertyRelative("Rows").intValue *
                                   property.FindPropertyRelative("Columns").intValue;
            };

            return root;
        }
        
        
        private void ChangeColumns(ChangeEvent<int> evt)
        {
            var uv = _preview.uv;
            uv.height = 1f / evt.newValue;
            _preview.uv = uv;
            _preview.MarkDirtyRepaint();
            _frameCount.text = $"Frame {_currentFrame} / " +
                               _property.FindPropertyRelative("Rows").intValue *
                               _property.FindPropertyRelative("Columns").intValue;
        }

        private void ChangeRows(ChangeEvent<int> evt)
        {
            var uv = _preview.uv;
            uv.width = 1f / evt.newValue;
            _preview.uv = uv;
            _preview.MarkDirtyRepaint();
            _frameCount.text = $"Frame {_currentFrame} / " +
                               _property.FindPropertyRelative("Rows").intValue *
                               _property.FindPropertyRelative("Columns").intValue;
        }

        private void ChangeAnimated(ChangeEvent<bool> evt)
        {
            _animatedProperties.SetEnabled(evt.newValue);
            if (evt.newValue)
            {
                var uv = _preview.uv;
                uv.width = 1f / _property.FindPropertyRelative("Rows").intValue;
                uv.height = 1f/ _property.FindPropertyRelative("Columns").intValue;
                _preview.uv = uv;
                _preview.MarkDirtyRepaint();
                _animatedProperties.RemoveFromClassList("hidden");
            }
            else
            {
                _animatedProperties.AddToClassList("hidden");
                var uv = _preview.uv;
                uv.width = 1f;
                uv.height = 1f;
                _preview.uv = uv;
                _preview.MarkDirtyRepaint();
            }
            
        }
        
        private void SetTexture(ChangeEvent<Object> evt)
        {
            //_preview.style.backgroundImage = evt.newValue as Texture2D;
            _preview.image = evt.newValue as Texture2D;
            Debug.Log($"Setting texture from {evt.previousValue} to {evt.newValue} on object {_preview.parent.parent.parent.parent.parent.parent.name}");
        }

    }
}