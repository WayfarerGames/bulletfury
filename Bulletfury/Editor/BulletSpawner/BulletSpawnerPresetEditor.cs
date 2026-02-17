using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BulletFury;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Wayfarer_Games.Common;

namespace Wayfarer_Games.BulletFury
{
    [CustomEditor(typeof(BulletSpawnerPreset))]
    public class BulletSpawnerPresetEditor : Editor
    {
        private SerializedProperty _useMainProperty;
        private SerializedProperty _mainProperty;
        private SerializedProperty _useShapeProperty;
        private SerializedProperty _shapeDataProperty;
        private SerializedProperty _useBurstDataProperty;
        private SerializedProperty _burstDataProperty;
        private SerializedProperty _useModulesProperty;
        private SerializedProperty _bulletModulesProperty;

        private VisualElement _modulesList;
        private VisualElement _addModulePicker;
        private TextField _addModuleSearchField;
        private VisualElement _addModuleResults;
        private List<Type> _availableModuleTypes = new();

        public override VisualElement CreateInspectorGUI()
        {
            CacheProperties();

            var root = new VisualElement();
            var style = AssetPathUtility.LoadAssetAtKnownRoots<StyleSheet>("Bulletfury/Editor/BulletSpawnerStyle.uss");
            if (style != null)
                root.styleSheets.Add(style);

            var mainContainer = new VisualElement();
            mainContainer.AddToClassList("main-body");
            var title = new Label("Bullet Spawner Preset");
            title.AddToClassList("section-main-title");
            mainContainer.Add(title);

            mainContainer.Add(CreateSettingsCard("Main", _useMainProperty, _mainProperty));
            mainContainer.Add(CreateSettingsCard("Spawn Shape", _useShapeProperty, _shapeDataProperty));
            mainContainer.Add(CreateSettingsCard("Burst Data", _useBurstDataProperty, _burstDataProperty));
            mainContainer.Add(CreateModulesCard());

            root.Add(mainContainer);
            return root;
        }

        private void CacheProperties()
        {
            _useMainProperty = serializedObject.FindProperty("UseMain");
            _mainProperty = serializedObject.FindProperty("Main");
            _useShapeProperty = serializedObject.FindProperty("UseShape");
            _shapeDataProperty = serializedObject.FindProperty("ShapeData");
            _useBurstDataProperty = serializedObject.FindProperty("UseBurstData");
            _burstDataProperty = serializedObject.FindProperty("BurstData");
            _useModulesProperty = serializedObject.FindProperty("UseModules");
            _bulletModulesProperty = serializedObject.FindProperty("BulletModules");
        }

        private VisualElement CreateSettingsCard(string headerTitle, SerializedProperty useProperty, SerializedProperty dataProperty)
        {
            var card = new VisualElement();
            card.AddToClassList("panel");
            card.AddToClassList("settings-card");
            card.AddToClassList("settings-card-light");

            var header = new VisualElement();
            header.AddToClassList("header");
            var headerLabel = new Label(headerTitle);
            headerLabel.AddToClassList("title");
            headerLabel.AddToClassList("subsection-title");
            header.Add(headerLabel);
            card.Add(header);

            var body = new VisualElement();
            body.AddToClassList("body");
            var useField = new PropertyField(useProperty);
            var dataField = new PropertyField(dataProperty);
            body.Add(useField);
            body.Add(dataField);
            card.Add(body);

            body.SetEnabled(useProperty.boolValue);
            useField.RegisterValueChangeCallback(_ => body.SetEnabled(useProperty.boolValue));
            return card;
        }

        private VisualElement CreateModulesCard()
        {
            var card = new VisualElement();
            card.AddToClassList("main-body");
            card.AddToClassList("modules-panel");

            var header = new VisualElement();
            header.AddToClassList("modules-header");
            var headerLabel = new Label("Modules");
            headerLabel.AddToClassList("section-main-title");
            header.Add(headerLabel);
            card.Add(header);

            var useModulesField = new PropertyField(_useModulesProperty);
            useModulesField.AddToClassList("modules-list");
            card.Add(useModulesField);

            _modulesList = new VisualElement();
            _modulesList.AddToClassList("modules-list");
            card.Add(_modulesList);

            var controls = new VisualElement();
            controls.AddToClassList("modules-controls");

            var addButton = new Button(ToggleAddModulePicker) { text = "Add Module" };
            addButton.AddToClassList("modules-add-button");
            controls.Add(addButton);

            _addModulePicker = new VisualElement();
            _addModulePicker.AddToClassList("add-module-picker");
            _addModulePicker.AddToClassList("collapsed");

            _addModuleSearchField = new TextField("Search");
            _addModuleSearchField.AddToClassList("add-module-search-field");
            _addModuleSearchField.RegisterValueChangedCallback(_ => RefreshAddModuleResults());
            _addModulePicker.Add(_addModuleSearchField);

            _addModuleResults = new VisualElement();
            _addModuleResults.AddToClassList("add-module-results");
            _addModulePicker.Add(_addModuleResults);
            controls.Add(_addModulePicker);

            card.Add(controls);

            var modulesEnabled = _useModulesProperty.boolValue;
            _modulesList.SetEnabled(modulesEnabled);
            controls.SetEnabled(modulesEnabled);
            useModulesField.RegisterValueChangeCallback(_ =>
            {
                var enabled = _useModulesProperty.boolValue;
                _modulesList.SetEnabled(enabled);
                controls.SetEnabled(enabled);
            });

            CacheAvailableModuleTypes();
            RefreshAddModuleResults();
            RebuildModuleList();
            return card;
        }

        private void CacheAvailableModuleTypes()
        {
            _availableModuleTypes = GetAllBulletModuleTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } &&
                               !type.ContainsGenericParameters &&
                               !typeof(UnityEngine.Object).IsAssignableFrom(type))
                .OrderBy(GetModuleDisplayName)
                .ToList();
        }

        private static IEnumerable<Type> GetAllBulletModuleTypes()
        {
            // Query all supported module interfaces so modules from any assembly/source are discovered.
            return TypeCache.GetTypesDerivedFrom<IBaseBulletModule>()
                .Concat(TypeCache.GetTypesDerivedFrom<IBulletModule>())
                .Concat(TypeCache.GetTypesDerivedFrom<IParallelBulletModule>())
                .Concat(TypeCache.GetTypesDerivedFrom<IBulletInitModule>())
                .Concat(TypeCache.GetTypesDerivedFrom<IBulletDieModule>())
                .Concat(TypeCache.GetTypesDerivedFrom<IBulletSpawnModule>())
                .Distinct();
        }

        private void ToggleAddModulePicker()
        {
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
                var noResultsLabel = new Label("No modules found");
                noResultsLabel.AddToClassList("add-module-empty");
                _addModuleResults.Add(noResultsLabel);
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

        private void RebuildModuleList()
        {
            if (_modulesList == null || _bulletModulesProperty == null)
                return;

            serializedObject.Update();
            _modulesList.Clear();

            for (int i = 0; i < _bulletModulesProperty.arraySize; i++)
            {
                var index = i;
                var moduleProp = _bulletModulesProperty.GetArrayElementAtIndex(index);

                var moduleItem = new VisualElement();
                moduleItem.AddToClassList("module-item");
                moduleItem.AddToClassList(index % 2 == 0 ? "module-item-light" : "module-item-dark");

                var header = new VisualElement();
                header.AddToClassList("module-item-header");

                var title = new Label(GetModuleDisplayName(moduleProp));
                title.AddToClassList("module-item-title");
                title.AddToClassList("subsection-title");
                header.Add(title);
                var moduleType = moduleProp.managedReferenceValue?.GetType();
                if (moduleType != null && typeof(IParallelBulletModule).IsAssignableFrom(moduleType))
                {
                    var parallelSafePill = new VisualElement();
                    parallelSafePill.AddToClassList("module-performance-pill");
                    parallelSafePill.AddToClassList("module-parallel-safe");
                    var parallelSafeLabel = new Label("Parallel Safe");
                    parallelSafeLabel.AddToClassList("module-performance-pill-text");
                    parallelSafePill.Add(parallelSafeLabel);
                    header.Add(parallelSafePill);
                }

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
        }

        private void AddModule(Type moduleType)
        {
            if (_bulletModulesProperty == null)
                return;

            object createdInstance;
            try
            {
                createdInstance = Activator.CreateInstance(moduleType);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "Unable to Add Module",
                    $"Could not create an instance of '{moduleType.FullName}'.\n\n" +
                    "Ensure the module has a public parameterless constructor (or no explicit constructor).\n\n" +
                    $"Details: {ex.Message}",
                    "OK");
                return;
            }

            serializedObject.Update();
            var index = _bulletModulesProperty.arraySize;
            _bulletModulesProperty.arraySize++;
            var moduleProp = _bulletModulesProperty.GetArrayElementAtIndex(index);
            moduleProp.managedReferenceValue = createdInstance;
            serializedObject.ApplyModifiedProperties();
            RebuildModuleList();
        }

        private void RemoveModuleAt(int index)
        {
            if (_bulletModulesProperty == null || index < 0 || index >= _bulletModulesProperty.arraySize)
                return;

            serializedObject.Update();
            var sizeBefore = _bulletModulesProperty.arraySize;
            _bulletModulesProperty.DeleteArrayElementAtIndex(index);
            if (_bulletModulesProperty.arraySize == sizeBefore && index < _bulletModulesProperty.arraySize)
                _bulletModulesProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            RebuildModuleList();
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

    }
}
