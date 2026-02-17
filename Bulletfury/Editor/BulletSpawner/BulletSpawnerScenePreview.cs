using System;
using System.Collections.Generic;
using System.Linq;
using BulletFury;
using UnityEditor;
using UnityEngine;

namespace Wayfarer_Games.BulletFury
{
    [InitializeOnLoad]
    internal static class BulletSpawnerScenePreview
    {
        private const float PopupWidth = 220f;
        private const float PopupHeight = 62f;
        private const float PopupMargin = 12f;

        private enum PreviewState
        {
            Stopped,
            Playing,
            Paused
        }

        private static readonly List<BulletSpawner> PreviewSpawners = new();
        private static PreviewState _state = PreviewState.Stopped;
        private static double _lastTickTime;
        private static int _maxActiveBullets;

        static BulletSpawnerScenePreview()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;

            RefreshSelection();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
                StopAndClearPreview();
        }

        private static void Cleanup()
        {
            StopAndClearPreview();
            PreviewSpawners.Clear();
        }

        private static void OnSelectionChanged()
        {
            RefreshSelection();
            if (PreviewSpawners.Count == 0)
                StopAndClearPreview();
        }

        private static void OnEditorUpdate()
        {
            if (Application.isPlaying || _state != PreviewState.Playing)
                return;

            RefreshSelection();
            if (PreviewSpawners.Count == 0)
            {
                StopAndClearPreview();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - _lastTickTime), 0f, 0.1f);
            _lastTickTime = now;
            if (deltaTime <= 0f)
                return;

            var sceneCamera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            var currentCount = 0;
            foreach (var spawner in PreviewSpawners)
            {
                if (spawner == null)
                    continue;

                spawner.EnsureSimulationInitialized();
                spawner.Play();
                spawner.UpdateAllBullets(sceneCamera, deltaTime);
                currentCount += spawner.BulletCount;
            }

            _maxActiveBullets = Mathf.Max(_maxActiveBullets, currentCount);
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Application.isPlaying)
                return;

            RefreshSelection();
            if (PreviewSpawners.Count == 0)
                return;

            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < PreviewSpawners.Count; i++)
                {
                    var spawner = PreviewSpawners[i];
                    if (spawner == null)
                        continue;

                    spawner.RenderBulletsNow();
                }
            }

            var currentCount = GetCurrentBulletCount();
            var popupRect = new Rect(
                sceneView.position.width - PopupWidth - PopupMargin,
                sceneView.position.height - PopupHeight - PopupMargin,
                PopupWidth,
                PopupHeight);

            Handles.BeginGUI();
            GUILayout.BeginArea(popupRect, EditorStyles.helpBox);

            GUILayout.BeginHorizontal();
            if (_state != PreviewState.Playing)
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton"), GUILayout.Width(28f), GUILayout.Height(18f)))
                    StartOrResumePreview();
            }

            if (_state == PreviewState.Playing)
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton"), GUILayout.Width(28f), GUILayout.Height(18f)))
                    PausePreview();
            }

            using (new EditorGUI.DisabledScope(_state == PreviewState.Stopped && currentCount == 0))
            {
                if (GUILayout.Button("Stop", GUILayout.Width(44f), GUILayout.Height(18f)))
                    StopAndClearPreview();
            }

            GUILayout.Space(6f);
            GUILayout.Label($"A:{currentCount}  M:{_maxActiveBullets}");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static int GetCurrentBulletCount()
        {
            var count = 0;
            for (int i = 0; i < PreviewSpawners.Count; i++)
            {
                var spawner = PreviewSpawners[i];
                if (spawner == null)
                    continue;
                count += spawner.BulletCount;
            }

            return count;
        }

        private static void StartOrResumePreview()
        {
            if (_state == PreviewState.Paused)
            {
                for (int i = 0; i < PreviewSpawners.Count; i++)
                    PreviewSpawners[i]?.Play();
            }
            else
            {
                _maxActiveBullets = 0;
                for (int i = 0; i < PreviewSpawners.Count; i++)
                {
                    var spawner = PreviewSpawners[i];
                    if (spawner == null)
                        continue;

                    spawner.EnsureSimulationInitialized();
                    spawner.ClearBullets();
                    spawner.Play();
                }
            }

            _lastTickTime = EditorApplication.timeSinceStartup;
            _state = PreviewState.Playing;
            SceneView.RepaintAll();
        }

        private static void PausePreview()
        {
            for (int i = 0; i < PreviewSpawners.Count; i++)
                PreviewSpawners[i]?.Stop();

            _state = PreviewState.Paused;
            SceneView.RepaintAll();
        }

        private static void StopAndClearPreview()
        {
            for (int i = 0; i < PreviewSpawners.Count; i++)
            {
                var spawner = PreviewSpawners[i];
                if (spawner == null)
                    continue;

                spawner.Stop();
                spawner.ClearBullets();
            }

            _state = PreviewState.Stopped;
            _maxActiveBullets = 0;
            SceneView.RepaintAll();
        }

        private static void RefreshSelection()
        {
            var selectedSpawners = Selection
                .GetFiltered<BulletSpawner>(SelectionMode.Editable | SelectionMode.ExcludePrefab)
                .Where(spawner => spawner != null && spawner.gameObject.scene.IsValid())
                .Distinct()
                .ToList();

            if (_state == PreviewState.Playing)
            {
                foreach (var spawner in selectedSpawners)
                {
                    if (PreviewSpawners.Contains(spawner))
                        continue;

                    spawner.EnsureSimulationInitialized();
                    spawner.ClearBullets();
                    spawner.Play();
                }
            }

            for (int i = 0; i < PreviewSpawners.Count; i++)
            {
                var spawner = PreviewSpawners[i];
                if (spawner != null && selectedSpawners.Contains(spawner))
                    continue;

                if (spawner != null)
                {
                    spawner.Stop();
                    spawner.ClearBullets();
                }
            }

            PreviewSpawners.Clear();
            PreviewSpawners.AddRange(selectedSpawners);
        }
    }
}
