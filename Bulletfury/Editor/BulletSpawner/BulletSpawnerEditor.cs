using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BulletFury;
using BulletFury.Data;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Wayfarer_Games.BulletFury.RenderData;
using Object = UnityEngine.Object;

namespace Wayfarer_Games.BulletFury
{
    [CustomEditor(typeof(BulletSpawner))]
    public class BulletSpawnerEditor : Editor
    {
        public VisualTreeAsset UXML;
        private VisualElement _root;
        private Image _preview, _sharedPreview;
        private VisualElement _animatedProperties;
        private VisualElement[] _colliderPreview, _sharedColliderPreview;
        private VisualElement _visualBody;
        private VisualElement _shapeBody;
        private VisualElement _group;
        private VisualElement _mainBody;
        private VisualElement _groupBody;
        private PropertyField _colliderSpacing;
        private HelpBox _shapeHelp;
        private Label _frameCount;
        private SharedRenderDataSO _data;
        private VisualElement _modulesList;
        private SerializedProperty _allModulesProperty;
        private Label _totalPerformanceImpactPill;
        private VisualElement _addModulePicker;
        private TextField _addModuleSearchField;
        private VisualElement _addModuleResults;
        private List<Type> _availableModuleTypes = new();

        private readonly struct ModulePerformanceInfo
        {
            public ModulePerformanceInfo(ModulePerformanceImpactRating rating, string justification = null)
            {
                Rating = rating;
                Justification = justification;
            }

            public ModulePerformanceImpactRating Rating { get; }
            public string Justification { get; }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            UXML.CloneTree(root);
            _root = root;
            BuildRenderData(ref root);
            BuildVisualData(ref root);
            BuildSpawnShape(ref root);
            BuildModules(ref root);
            return root;
        }

        //
        // private void BuildModules(ref VisualElement root)
        // {
        //     var spawnModules = serializedObject.FindProperty("spawnModules");
        //     var spawnModulesRoot = root.Q<ListView>("SpawnModules");
        //     spawnModulesRoot.itemsRemoved += SpawnModulesRootOnItemsRemoved;
        // }
        //
        // private void SpawnModulesRootOnItemsRemoved(IEnumerable<int> obj)
        // {
        //     var spawnModules = serializedObject.FindProperty("spawnModules");
        //     var spawnModulesRoot = _root.Q<ListView>("SpawnModules");
        //
        //     spawnModules.serializedObject.Update();
        //     spawnModules.serializedObject.ApplyModifiedProperties();
        //     spawnModulesRoot.Rebuild();
        // }

        private void BuildSpawnShape(ref VisualElement root)
        {
            root.Q<VisualElement>("ShapeHeader").RegisterCallback<ClickEvent>(ToggleShape);
            root.Q<VisualElement>("MainHeader").RegisterCallback<ClickEvent>(ToggleMain);
            root.Q<VisualElement>("GroupHeader").RegisterCallback<ClickEvent>(ToggleGroupData);
            root.Q<PropertyField>("SpawnDir").RegisterValueChangeCallback(ChangeSpawnDir);
            _shapeHelp = root.Q<HelpBox>("SpawnDirHelp");
            _shapeBody = root.Q<VisualElement>("ShapeBody");
            _group = root.Q<VisualElement>("Group");
            _mainBody = root.Q<VisualElement>("VisualBody");
            _groupBody = root.Q<VisualElement>("GroupBody");

            root.Q<PropertyField>("NumPerSide").RegisterCallback<ChangeEvent<int>>(ChangeNumPerSide);

            var prop = serializedObject.FindProperty("spawnShapeData").FindPropertyRelative("spawnDir");
            if (Enum.TryParse<SpawnDir>(prop.enumNames[prop.enumValueIndex], out var dir))
            {
                _shapeHelp.text = dir switch
                {
                    SpawnDir.Shape =>
                        "Bullets will travel in the direction of the shape's edge, e.g. for a square, they will travel up, down, left, or right.",
                    SpawnDir.Randomised => "Bullets will travel in a random direction",
                    SpawnDir.Spherised =>
                        "Bullets will travel away from the center of the shape, which will create a circular pattern",
                    SpawnDir.Direction =>
                        $"Bullets will travel in the direction of the spawner's UP vector - the green arrow in the scene view.",
                    SpawnDir.Point =>
                        "Bullets will travel in the direction of shape's vertex, e.g. for a square, they will travel in the diagonal directions.",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        private void BuildModules(ref VisualElement root)
        {
            _allModulesProperty = serializedObject.FindProperty("allModules");
            _modulesList = root.Q<VisualElement>("ModulesList");
            _totalPerformanceImpactPill = root.Q<Label>("TotalPerformanceImpactPill");
            _addModulePicker = root.Q<VisualElement>("AddModulePicker");
            _addModuleSearchField = root.Q<TextField>("AddModuleSearchField");
            _addModuleResults = root.Q<VisualElement>("AddModuleResults");

            var addButton = root.Q<Button>("AddModuleButton");
            if (addButton != null)
                addButton.clicked += ToggleAddModulePicker;

            if (_addModuleSearchField != null)
            {
                _addModuleSearchField.SetValueWithoutNotify(string.Empty);
                _addModuleSearchField.RegisterValueChangedCallback(_ => RefreshAddModuleResults());
            }

            CacheAvailableModuleTypes();
            RefreshAddModuleResults();

            RebuildModuleList();
        }

        private void RebuildModuleList()
        {
            if (_modulesList == null || _allModulesProperty == null)
                return;

            serializedObject.Update();
            _modulesList.Clear();

            for (int i = 0; i < _allModulesProperty.arraySize; i++)
            {
                var index = i;
                var moduleProp = _allModulesProperty.GetArrayElementAtIndex(index);
                var moduleItem = new VisualElement();
                moduleItem.AddToClassList("module-item");
                moduleItem.AddToClassList(index % 2 == 0 ? "module-item-light" : "module-item-dark");

                var header = new VisualElement();
                header.AddToClassList("module-item-header");

                var title = new Label(GetModuleDisplayName(moduleProp));
                title.AddToClassList("module-item-title");
                title.AddToClassList("subsection-title");
                var moduleType = moduleProp.managedReferenceValue?.GetType();
                var isParallelSafe = moduleType != null && typeof(IParallelBulletModule).IsAssignableFrom(moduleType);
                var modulePerformanceInfo = GetModulePerformanceInfo(moduleProp);
                var moduleRating = modulePerformanceInfo.Rating;
                if (moduleRating >= ModulePerformanceImpactRating.Medium || isParallelSafe)
                {
                    header.Add(title);
                    if (isParallelSafe)
                    {
                        var parallelSafePill = new VisualElement();
                        parallelSafePill.AddToClassList("module-performance-pill");
                        parallelSafePill.AddToClassList("module-parallel-safe");
                        var parallelSafeLabel = new Label("Parallel Safe");
                        parallelSafeLabel.AddToClassList("module-performance-pill-text");
                        parallelSafePill.Add(parallelSafeLabel);
                        header.Add(parallelSafePill);
                    }

                    if (moduleRating >= ModulePerformanceImpactRating.Medium)
                    {
                        var moduleImpact = new VisualElement();
                        moduleImpact.AddToClassList("module-performance-pill");
                        moduleImpact.AddToClassList(GetRatingClass(moduleRating));
                        var moduleImpactLabel = new Label($"Performance Impact: {FormatRating(moduleRating)}");
                        moduleImpactLabel.AddToClassList("module-performance-pill-text");
                        moduleImpact.Add(moduleImpactLabel);

                        var moduleInfoIcon = new Label("?");
                        moduleInfoIcon.AddToClassList("module-performance-help-icon");
                        moduleInfoIcon.tooltip = GetPerformanceJustification(modulePerformanceInfo, moduleRating);
                        moduleInfoIcon.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                        moduleImpact.Add(moduleInfoIcon);
                        header.Add(moduleImpact);
                    }
                }
                else
                    header.Add(title);

                var removeButton = new Button(() => RemoveModuleAt(index)) { text = "Remove" };
                removeButton.AddToClassList("module-remove-button");
                removeButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

                header.Add(removeButton);
                moduleItem.Add(header);

                var content = new VisualElement();
                content.AddToClassList("module-item-content");
                AddModuleFields(moduleProp, content);
                moduleItem.Add(content);
                header.RegisterCallback<ClickEvent>(_ => content.ToggleInClassList("collapsed"));

                _modulesList.Add(moduleItem);
            }

            UpdateTotalPerformanceImpactPill();
        }

        private static void AddModuleFields(SerializedProperty moduleProp, VisualElement moduleItem)
        {
            if (moduleProp == null || moduleProp.managedReferenceValue == null)
                return;

            var iterator = moduleProp.Copy();
            var end = moduleProp.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                var childProperty = iterator.Copy();
                var childField = new PropertyField(childProperty);
                childField.BindProperty(childProperty);
                moduleItem.Add(childField);
            }
        }

        private void CacheAvailableModuleTypes()
        {
            _availableModuleTypes = TypeCache.GetTypesDerivedFrom<IBaseBulletModule>()
                .Where(type => type is { IsClass: true, IsAbstract: false } &&
                               !type.ContainsGenericParameters &&
                               !typeof(UnityEngine.Object).IsAssignableFrom(type) &&
                               type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(GetModuleDisplayName)
                .ToList();
        }

        private void ToggleAddModulePicker()
        {
            if (_addModulePicker == null)
                return;

            var shouldOpen = _addModulePicker.ClassListContains("collapsed");
            if (shouldOpen)
            {
                CacheAvailableModuleTypes();
                RefreshAddModuleResults();
                _addModulePicker.RemoveFromClassList("collapsed");
                _addModuleSearchField?.Focus();
                return;
            }

            HideAddModulePicker(true);
        }

        private void HideAddModulePicker(bool clearSearch)
        {
            if (_addModulePicker == null)
                return;

            _addModulePicker.AddToClassList("collapsed");
            if (!clearSearch || _addModuleSearchField == null)
                return;

            _addModuleSearchField.SetValueWithoutNotify(string.Empty);
            RefreshAddModuleResults();
        }

        private void RefreshAddModuleResults()
        {
            if (_addModuleResults == null)
                return;

            _addModuleResults.Clear();
            var searchText = _addModuleSearchField?.value?.Trim() ?? string.Empty;
            var filteredModules = _availableModuleTypes;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredModules = _availableModuleTypes
                    .Where(moduleType =>
                        GetModuleDisplayName(moduleType).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (filteredModules.Count == 0)
            {
                _addModuleResults.Add(new Label("No modules found") { name = "NoModulesFoundLabel" });
                var noModulesLabel = _addModuleResults.Q<Label>("NoModulesFoundLabel");
                noModulesLabel?.AddToClassList("add-module-empty");
                return;
            }

            foreach (var moduleType in filteredModules)
            {
                var capturedType = moduleType;
                var moduleButton = new Button(() =>
                {
                    AddModule(capturedType);
                    HideAddModulePicker(true);
                })
                {
                    tooltip = capturedType.FullName
                };

                var title = new Label(GetModuleDisplayName(capturedType));
                title.AddToClassList("add-module-item-title");
                moduleButton.Add(title);

                var description = new Label(GetModuleShortDescription(capturedType));
                description.AddToClassList("add-module-item-description");
                moduleButton.Add(description);

                moduleButton.AddToClassList("add-module-item");
                _addModuleResults.Add(moduleButton);
            }
        }

        private static string GetModuleShortDescription(Type moduleType)
        {
            if (moduleType == null)
                return "Module behavior.";

            var attribute = (ModuleDescriptionAttribute)Attribute.GetCustomAttribute(
                moduleType,
                typeof(ModuleDescriptionAttribute),
                true);
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Description))
                return attribute.Description;

            return "Custom module behavior.";
        }

        private void AddModule(Type moduleType)
        {
            if (_allModulesProperty == null) return;

            serializedObject.Update();
            var index = _allModulesProperty.arraySize;
            _allModulesProperty.arraySize++;
            var moduleProp = _allModulesProperty.GetArrayElementAtIndex(index);
            moduleProp.managedReferenceValue = Activator.CreateInstance(moduleType);
            serializedObject.ApplyModifiedProperties();
            RebuildModuleList();
        }

        private void RemoveModuleAt(int index)
        {
            if (_allModulesProperty == null || index < 0 || index >= _allModulesProperty.arraySize)
                return;

            serializedObject.Update();
            var sizeBefore = _allModulesProperty.arraySize;
            _allModulesProperty.DeleteArrayElementAtIndex(index);
            if (_allModulesProperty.arraySize == sizeBefore && index < _allModulesProperty.arraySize)
                _allModulesProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            RebuildModuleList();
        }

        private static string GetModuleDisplayName(SerializedProperty moduleProp)
        {
            var instance = moduleProp?.managedReferenceValue;
            if (instance != null)
                return GetModuleDisplayName(instance.GetType());

            return "Unassigned Module";
        }

        private static string GetModuleDisplayName(Type moduleType)
        {
            if (moduleType == null)
                return "Module";

            var name = moduleType.Name;
            if (name.EndsWith("Module", StringComparison.Ordinal))
                name = name[..^"Module".Length];

            name = Regex.Replace(name, "(\\B[A-Z])", " $1");
            return string.IsNullOrWhiteSpace(name) ? "Module" : name.Trim();
        }

        private static string GetRatingClass(ModulePerformanceImpactRating rating)
        {
            return rating switch
            {
                ModulePerformanceImpactRating.High => "module-performance-high",
                ModulePerformanceImpactRating.VeryHigh => "module-performance-very-high",
                _ => "module-performance-medium"
            };
        }

        private static string FormatRating(ModulePerformanceImpactRating rating)
        {
            return rating switch
            {
                ModulePerformanceImpactRating.VeryHigh => "Very High",
                _ => rating.ToString()
            };
        }

        private static int GetRatingWeight(ModulePerformanceImpactRating rating)
        {
            return rating switch
            {
                ModulePerformanceImpactRating.Low => 1,
                ModulePerformanceImpactRating.Medium => 2,
                ModulePerformanceImpactRating.High => 4,
                ModulePerformanceImpactRating.VeryHigh => 6,
                _ => 2
            };
        }

        private static ModulePerformanceInfo GetTypePerformanceInfo(Type moduleType)
        {
            if (moduleType == null)
                return new ModulePerformanceInfo(ModulePerformanceImpactRating.Medium, "Module type is unresolved, so impact is treated as medium by default.");

            var attribute = (ModulePerformanceImpactAttribute)Attribute.GetCustomAttribute(
                moduleType,
                typeof(ModulePerformanceImpactAttribute),
                true);
            if (attribute != null)
                return new ModulePerformanceInfo(attribute.Rating, attribute.Justification);

            return new ModulePerformanceInfo(ModulePerformanceImpactRating.Medium,
                "Custom module with unknown runtime cost; add ModulePerformanceImpact to document expected impact.");
        }

        private static ModulePerformanceInfo GetModulePerformanceInfo(SerializedProperty moduleProp)
        {
            var moduleType = moduleProp?.managedReferenceValue?.GetType();
            return GetTypePerformanceInfo(moduleType);
        }

        private static string GetPerformanceJustification(ModulePerformanceInfo moduleInfo,
            ModulePerformanceImpactRating rating)
        {
            if (!string.IsNullOrWhiteSpace(moduleInfo.Justification))
                return moduleInfo.Justification;

            return rating switch
            {
                ModulePerformanceImpactRating.Medium =>
                    "Adds measurable per-bullet overhead that may matter at higher bullet counts.",
                ModulePerformanceImpactRating.High =>
                    "Adds significant per-bullet or per-collision work that scales quickly.",
                ModulePerformanceImpactRating.VeryHigh =>
                    "Can multiply active bullets or trigger heavy repeated processing.",
                _ => "Low-impact by default."
            };
        }

        private ModulePerformanceImpactRating GetSpawnerPerformanceRating()
        {
            if (_allModulesProperty == null || _allModulesProperty.arraySize == 0)
                return ModulePerformanceImpactRating.Low;

            var totalWeight = 0;
            for (int i = 0; i < _allModulesProperty.arraySize; i++)
            {
                var moduleProp = _allModulesProperty.GetArrayElementAtIndex(i);
                totalWeight += GetRatingWeight(GetModulePerformanceInfo(moduleProp).Rating);
            }

            return totalWeight switch
            {
                <= 3 => ModulePerformanceImpactRating.Low,
                <= 7 => ModulePerformanceImpactRating.Medium,
                <= 12 => ModulePerformanceImpactRating.High,
                _ => ModulePerformanceImpactRating.VeryHigh
            };
        }

        private void UpdateTotalPerformanceImpactPill()
        {
            if (_totalPerformanceImpactPill == null)
                return;

            var rating = GetSpawnerPerformanceRating();
            _totalPerformanceImpactPill.RemoveFromClassList("module-performance-medium");
            _totalPerformanceImpactPill.RemoveFromClassList("module-performance-high");
            _totalPerformanceImpactPill.RemoveFromClassList("module-performance-very-high");
            if (rating > ModulePerformanceImpactRating.Medium)
            {
                _totalPerformanceImpactPill.text = $"\u26A0 Total Performance Impact: {FormatRating(rating)}";
                _totalPerformanceImpactPill.AddToClassList(GetRatingClass(rating));
                _totalPerformanceImpactPill.RemoveFromClassList("collapsed");
                return;
            }

            _totalPerformanceImpactPill.AddToClassList("collapsed");
        }

        private void ChangeNumPerSide(ChangeEvent<int> evt)
        {
            if (evt.newValue == 1)
                _group.RemoveFromClassList("collapsed");
            else
                _group.AddToClassList("collapsed");
        }

        private void ChangeSpawnDir(SerializedPropertyChangeEvent evt)
        {
            if (Enum.TryParse<SpawnDir>(evt.changedProperty.enumNames[evt.changedProperty.enumValueIndex], out var dir))
            {
                _shapeHelp.text = dir switch
                {
                    SpawnDir.Shape =>
                        "Bullets will travel in the direction of the shape's edge, e.g. for a square, they will travel up, down, left, or right.",
                    SpawnDir.Randomised => "Bullets will travel in a random direction",
                    SpawnDir.Spherised =>
                        "Bullets will travel away from the center of the shape, which will create a circular pattern",
                    SpawnDir.Direction =>
                        $"Bullets will travel in the direction of the spawner's UP vector - the green arrow in the scene view.",
                    SpawnDir.Point =>
                        "Bullets will travel in the direction of shape's vertex, e.g. for a square, they will travel in the diagonal directions.",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        private void BuildVisualData(ref VisualElement root)
        {
            root.Q<PropertyField>("StartColor").RegisterValueChangeCallback(ChangeColor);
            root.Q<PropertyField>("ColliderSize").RegisterCallback<ChangeEvent<float>>(ChangeColliderSize);
            //root.Q<VisualElement>("VisualHeader").RegisterCallback<ClickEvent>(ToggleVisual);
            //_visualBody = root.Q<VisualElement>("VisualBody");

            root.Q<PropertyField>("ColliderType").RegisterCallback<ChangeEvent<string>>(ChangeColliderCount);
            _colliderSpacing = root.Q<PropertyField>("ColliderSeparation");
            _colliderSpacing.RegisterCallback<ChangeEvent<float>>(ChangeColliderSeparation);
        }

        private void GrabPreviewAndChildren(VisualElement root)
        {
            if (_root == null) return;
            (_preview, _sharedPreview) = root.Q<SharedRenderDataVisualElement>().GetPreview();
            (_colliderPreview, _sharedColliderPreview) = root.Q<SharedRenderDataVisualElement>().GetColliderPreview();
        }

        private void ChangeColliderSeparation(ChangeEvent<float> evt)
        {
            GrabPreviewAndChildren(_root);
            if (_preview == null) return;

            var useCollider =
                serializedObject.FindProperty("main").FindPropertyRelative("ColliderType").enumValueIndex ==
                (int)ColliderType.Capsule;
            if (useCollider)
            {
                for (int i = 0; i < _colliderPreview.Length; ++i)
                {
                    var pos = _colliderPreview[i].transform.position;
                    pos.y = evt.newValue * (i - 0.5f) * 100;
                    _colliderPreview[i].transform.position = pos;
                }

                for (int i = 0; i < _sharedColliderPreview.Length; ++i)
                {
                    var pos = _sharedColliderPreview[i].transform.position;
                    pos.y = evt.newValue * (i - 0.5f) * 100;
                    _sharedColliderPreview[i].transform.position = pos;
                }
            }
        }

        private void ChangeColliderCount(ChangeEvent<string> evt)
        {
            GrabPreviewAndChildren(_root);
            if (_preview == null) return;

            if (evt.newValue == ColliderType.Capsule.ToString())
                _colliderSpacing.RemoveFromClassList("collapsed");
            else
                _colliderSpacing.AddToClassList("collapsed");

            var element = _colliderPreview[1];
            if (evt.newValue == ColliderType.Capsule.ToString())
                element.RemoveFromClassList("collapsed");
            else
                element.AddToClassList("collapsed");

            element = _sharedColliderPreview[1];
            if (evt.newValue == ColliderType.Capsule.ToString())
                element.RemoveFromClassList("collapsed");
            else
                element.AddToClassList("collapsed");

            var spacing = serializedObject.FindProperty("main").FindPropertyRelative("CapsuleLength").floatValue;

            if (evt.newValue == ColliderType.Capsule.ToString())
            {
                for (int i = 0; i < _colliderPreview.Length; ++i)
                {
                    var pos = _colliderPreview[i].transform.position;
                    pos.y = spacing * (i - 0.5f) * 100;
                    _colliderPreview[i].transform.position = pos;
                }

                for (int i = 0; i < _sharedColliderPreview.Length; ++i)
                {
                    var pos = _sharedColliderPreview[i].transform.position;
                    pos.y = spacing * (i - 0.5f) * 100;
                    _sharedColliderPreview[i].transform.position = pos;
                }
            }
            else
            {
                var pos = _colliderPreview[0].transform.position;
                pos.y = 0;
                _colliderPreview[0].transform.position = pos;

                pos = _sharedColliderPreview[0].transform.position;
                pos.y = 0;
                _sharedColliderPreview[0].transform.position = pos;
            }
        }

        private void ToggleShape(ClickEvent evt)
        {
            _shapeBody.ToggleInClassList("collapsed");
        }

        private void ToggleMain(ClickEvent evt)
        {
            _mainBody?.ToggleInClassList("collapsed");
        }

        private void ToggleGroupData(ClickEvent evt)
        {
            _groupBody?.ToggleInClassList("collapsed");
        }

        private void ToggleVisual(ClickEvent evt)
        {
            _visualBody.ToggleInClassList("collapsed");
        }

        private void ChangeColliderSize(ChangeEvent<float> evt)
        {
            GrabPreviewAndChildren(_root);
            if (_preview == null) return;

            foreach (var p in _colliderPreview)
                p.transform.scale = Vector3.one * evt.newValue;

            foreach (var p in _sharedColliderPreview)
                p.transform.scale = Vector3.one * evt.newValue;
        }

        private void ChangeColor(SerializedPropertyChangeEvent evt)
        {
            GrabPreviewAndChildren(_root);
            if (_preview == null) return;

            if (evt?.changedProperty == null) return;
            var color = evt.changedProperty.colorValue;
            _preview.tintColor = color;
            _sharedPreview.tintColor = color;
        }

        private void BuildRenderData(ref VisualElement root)
        {
            root.Q<PropertyField>("Script").SetEnabled(false);
            var renderData = serializedObject.FindProperty("renderData");
            var singleData = renderData.FindPropertyRelative("singleData");

            _data = (target as BulletSpawner).RenderData.SharedDataSO;


            var sharedRenderDataVisualElement = root.Q<SharedRenderDataVisualElement>();

            if (_data != null)
            {
                var targetObj = new SerializedObject(_data);
                sharedRenderDataVisualElement.InitWithProperties(singleData, targetObj.FindProperty("data"));
            }
            else
                sharedRenderDataVisualElement.InitWithProperties(singleData, null);

            sharedRenderDataVisualElement.SharedDataChanged += SharedDataChanged;

        }

        private void SharedDataChanged(ChangeEvent<Object> obj)
        {
            if (obj.newValue == null)
            {
                _data = null;
                return;
            }

            _data = obj.newValue as SharedRenderDataSO;
        }
    }
}