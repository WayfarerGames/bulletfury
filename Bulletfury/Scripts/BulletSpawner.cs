using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BulletFury.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace BulletFury
{
    public interface IBulletHitHandler
    {
        public void Hit(BulletContainer bullet);
    }
    
    public interface IBaseBulletModule { }

    public interface IBulletModule : IBaseBulletModule
    {
        public void Execute(ref BulletContainer container, float deltaTime);
    }

    /// <summary>
    /// Optional module interface for multithreaded execution.
    /// Keep implementations stateless and avoid Unity API calls inside Execute.
    /// </summary>
    public interface IParallelBulletModule : IBulletModule
    {
    }
    
    public interface IBulletInitModule : IBaseBulletModule
    {
        public void Execute(ref BulletContainer container);
    }

    public interface IBulletDieModule : IBaseBulletModule
    {
        public enum CollisionBehaviour { Dies, StaysAlive }
        public CollisionBehaviour Execute(ref BulletContainer container, bool isCollision, GameObject collidingObject);
    }

    public interface IBulletSpawnModule : IBaseBulletModule
    {
        public void Execute(ref Vector3 position, ref Quaternion rotation, float deltaTime);
    }

    [DefaultExecutionOrder(-1000)]
    public class BulletSpawner : MonoBehaviour
    {
        private const int MaxBullets = 10000;
        private const int MaxColliderHitsPerBullet = 4;
        
        [SerializeField] private SharedRenderData renderData;
        [SerializeField] private BulletMainData main;
        [SerializeField] private SpawnShapeData spawnShapeData;
        
        [SerializeField] private BurstData burstData;
        
        [SerializeReference]
        private List<IBaseBulletModule> allModules = new ();

        private bool _isStopped = false;
        private bool _simulationPaused = false;
        
        // Unity Event that fires when a bullet reaches end-of-life, can be set in the inspector like a button 
        // ReSharper disable once InconsistentNaming
        [SerializeField] private BulletDiedEvent OnBulletDied;
        public event Action<BulletContainer, bool> OnBulletDiedEvent;

        // Unity Event that fires when a bullet is spawned, can be set in the inspector like a button 
        // ReSharper disable once InconsistentNaming
        [SerializeField] private BulletSpawnedEvent OnBulletSpawned;
        public event Action<int, BulletContainer> OnBulletSpawnedEvent;
        
        [SerializeField] private UnityEvent OnWeaponFired;
        public event Action OnWeaponFiredEvent;
        
        public SharedRenderData RenderData => renderData;
        public BulletMainData Main => main;
        public SpawnShapeData SpawnShapeData => spawnShapeData;
        public BurstData BurstData => burstData;
        public float LastSimulationDeltaTime => _runtime.LastSimulationDeltaTime;

        private bool _enabled = true;
        private Vector3 _previousPos, _previousRot;
        private bool _hasSpawnedSinceEnable = false;
        private int _bulletCount;
        public int BulletCount => _bulletCount;
        private bool _bulletsFree = true;
        
        private Collider2D[] _hit = new Collider2D[MaxColliderHitsPerBullet];
        private ContactFilter2D _filter;
        private static readonly IBulletHitHandler[] EmptyHitHandlers = Array.Empty<IBulletHitHandler>();
        private static readonly ColliderInstanceIdComparer ColliderComparer = new();
        private static readonly BulletHitHandlerInstanceComparer HandlerComparer = new();
        private readonly Dictionary<int, IBulletHitHandler[]> _handlerCache = new();
        private readonly List<int> _handlerCacheKeysToRemove = new(64);
        private readonly List<HitDispatchRecord> _hitDispatchQueue = new(256);
        private readonly List<IParallelBulletModule> _parallelBulletModules = new();
        private readonly List<IBulletModule> _mainThreadBulletModules = new();
        private BulletContainer[] _parallelModuleBuffer = Array.Empty<BulletContainer>();
        private bool _moduleExecutionCachesDirty = true;

        private NativeArray<BulletContainer> _bullets;
        private NativeArray<Matrix4x4> _bulletTransforms;
        private NativeArray<float4> _bulletColors;
        private NativeArray<float> _bulletTimes;
        
        private (BulletRenderData renderData, Camera cam)? _queuedRenderData;

        private readonly BulletSpawnerRuntime _runtime = new();
        private readonly List<Vector3> _spawnPositions = new(256);
        private readonly List<Quaternion> _spawnRotations = new(256);
        private float _fireCooldownRemaining;
        private float _nextShotFireRate;
        private float _activeShotFireRate;
        private float _spawnSequenceDeltaTime;
        private bool _spawnSequenceActive;
        private bool _spawnSequenceIgnoreFireRate;
        private int _spawnSequenceBurstIndex;
        private float _spawnSequenceBurstDelayRemaining;
        private Transform _spawnSequenceOriginTransform;
        private Vector3 _spawnSequenceFallbackPosition;
        private Vector3 _spawnSequenceFallbackUp;
        private bool _disposed = false;
        public bool Disposed => _disposed;
        private int _handlerCachePruneCounter;
        
        private struct HitDispatchRecord
        {
            public BulletContainer Bullet;
            public IBulletHitHandler[] Handlers;
        }

        public struct RenderQueueData
        {
            public BulletRenderData RenderData;
            public Camera Camera;
            public int Count;
            public NativeArray<Matrix4x4> Transforms;
            public NativeArray<float4> Colors;
            public NativeArray<float> Times;
            public BulletSpawner Spawner;
        }
        private static SortedList<float, RenderQueueData> _renderQueue = new();
        public static SortedList<float, RenderQueueData> RenderQueue => _renderQueue;

        [Serializable]
        public sealed class SpawnerState
        {
            public int BulletCount;
            public BulletContainer[] Bullets = Array.Empty<BulletContainer>();
            public Vector3 PreviousPosition;
            public Vector3 PreviousRotation;
            public bool IsStopped;
            public bool HasSpawnedSinceEnable;
            public float FireCooldownRemaining;
            public float NextShotFireRate;
            public float ActiveShotFireRate;
            public bool SpawnSequenceActive;
            public bool SpawnSequenceIgnoreFireRate;
            public int SpawnSequenceBurstIndex;
            public float SpawnSequenceBurstDelayRemaining;
            public Transform SpawnSequenceOriginTransform;
            public Vector3 SpawnSequenceFallbackPosition;
            public Vector3 SpawnSequenceFallbackUp;
            public float SpawnSequenceDeltaTime;
            public object RuntimeState;
        }

        public void Start()
        {
            EnsureSimulationInitialized();
            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer),
                useTriggers = true
            };

        }

        private void OnEnable()
        {
            _isStopped = !main.PlayOnEnable;
            ResetSpawnSchedulingState();
        }

        public void SetPreset(BulletSpawnerPreset preset)
        {
            if (preset.UseMain)
                main = preset.Main;
            if (preset.UseShape)
                spawnShapeData = preset.ShapeData;
            if (preset.UseBurstData)
                burstData = preset.BurstData;
            if (preset.UseModules)
                allModules = CloneModules(preset.BulletModules);
            _moduleExecutionCachesDirty = true;
        }

        [Serializable]
        private class ModuleCloneWrapper
        {
            [SerializeReference] public IBaseBulletModule Module;
        }

        private static List<IBaseBulletModule> CloneModules(List<IBaseBulletModule> source)
        {
            if (source == null || source.Count == 0)
                return new List<IBaseBulletModule>();

            var clonedModules = new List<IBaseBulletModule>(source.Count);
            foreach (var module in source)
                clonedModules.Add(CloneModule(module));

            return clonedModules;
        }

        private static IBaseBulletModule CloneModule(IBaseBulletModule source)
        {
            if (source == null)
                return null;

            var wrapper = new ModuleCloneWrapper { Module = source };
            var json = JsonUtility.ToJson(wrapper);
            var cloned = JsonUtility.FromJson<ModuleCloneWrapper>(json);
            return cloned?.Module;
        }
        
        public void OnDestroy()
        {
            if (_disposed) return;
            if (_bullets.IsCreated)
                _bullets.Dispose();
            if (_bulletTransforms.IsCreated)
                _bulletTransforms.Dispose();
            if (_bulletColors.IsCreated)
                _bulletColors.Dispose();
            if (_bulletTimes.IsCreated)
                _bulletTimes.Dispose();
            _handlerCache.Clear();
            _hitDispatchQueue.Clear();
            _disposed = true;
        }

        public void Stop()
        {
            _isStopped = true;
        }

        public void Play()
        {
            _isStopped = false;
        }

        public void SetSimulationPaused(bool paused)
        {
            _simulationPaused = paused;
        }

        private void ResetSpawnerRuntimeState()
        {
            if (_moduleExecutionCachesDirty)
                RebuildModuleExecutionCaches();

            _previousPos = transform.position;
            _previousRot = transform.eulerAngles;
            _queuedRenderData = null;
            _runtime.ResetRuntimeState();
            ResetSpawnSchedulingState();
        }

        private void ResetSpawnSchedulingState()
        {
            _spawnSequenceActive = false;
            _spawnSequenceIgnoreFireRate = false;
            _spawnSequenceBurstIndex = 0;
            _spawnSequenceBurstDelayRemaining = 0f;
            _spawnSequenceOriginTransform = null;
            _spawnSequenceFallbackPosition = Vector3.zero;
            _spawnSequenceFallbackUp = Vector3.up;
            _spawnSequenceDeltaTime = 0f;
            _activeShotFireRate = 0f;
            _nextShotFireRate = SampleFireRate();
            _fireCooldownRemaining = Mathf.Max(0f, burstData.delay);
            _hasSpawnedSinceEnable = false;
        }

        private float SampleFireRate()
        {
            if (_moduleExecutionCachesDirty)
                RebuildModuleExecutionCaches();

            return Mathf.Max(0f, _runtime.Sample(main.FireRate));
        }

        public void EnsureSimulationInitialized()
        {
            if (_bullets.IsCreated && !_disposed)
                return;

            var count = burstData.maxActiveBullets == 0 ? MaxBullets : burstData.maxActiveBullets;
            _bullets =
                new NativeArray<BulletContainer>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _bulletTransforms =
                new NativeArray<Matrix4x4>(count, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _bulletColors =
                new NativeArray<float4>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _bulletTimes =
                new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            _disposed = false;
            for (int i = 0; i < _bullets.Length; i++)
            {
                _bullets[i] = new BulletContainer
                {
                    Id = i,
                    Dead = 1,
                    Position = float3.zero,
                    Rotation = Quaternion.identity,
                    CurrentSize = 1
                };
                _bulletTransforms[i] = Matrix4x4.zero;
                _bulletColors[i] = float4.zero;
                _bulletTimes[i] = 0f;
            }

            _bulletCount = 0;
            _isStopped = !main.PlayOnEnable;
            ResetSpawnerRuntimeState();
            _filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.GetLayerCollisionMask(gameObject.layer),
                useTriggers = true
            };
        }

        public void ClearBullets()
        {
            if (_disposed || !_bullets.IsCreated)
                return;

            for (int i = 0; i < _bulletCount; i++)
            {
                var bullet = _bullets[i];
                bullet.Dead = 1;
                bullet.EndOfLife = 0;
                _bullets[i] = bullet;
                _bulletTransforms[i] = Matrix4x4.zero;
                _bulletColors[i] = float4.zero;
                _bulletTimes[i] = 0f;
            }

            _bulletCount = 0;
            ResetSpawnerRuntimeState();
            _hitDispatchQueue.Clear();
            _handlerCache.Clear();
            _queuedRenderData = null;
        }

        public SpawnerState CaptureState(SpawnerState reusableState = null)
        {
            EnsureSimulationInitialized();
            if (_moduleExecutionCachesDirty)
                RebuildModuleExecutionCaches();

            var state = reusableState ?? new SpawnerState();
            state.BulletCount = _bulletCount;

            if (state.Bullets == null || state.Bullets.Length < _bulletCount)
                state.Bullets = new BulletContainer[_bulletCount];
            for (int i = 0; i < _bulletCount; i++)
                state.Bullets[i] = _bullets[i];

            state.PreviousPosition = _previousPos;
            state.PreviousRotation = _previousRot;
            state.IsStopped = _isStopped;
            state.HasSpawnedSinceEnable = _hasSpawnedSinceEnable;
            state.FireCooldownRemaining = _fireCooldownRemaining;
            state.NextShotFireRate = _nextShotFireRate;
            state.ActiveShotFireRate = _activeShotFireRate;
            state.SpawnSequenceActive = _spawnSequenceActive;
            state.SpawnSequenceIgnoreFireRate = _spawnSequenceIgnoreFireRate;
            state.SpawnSequenceBurstIndex = _spawnSequenceBurstIndex;
            state.SpawnSequenceBurstDelayRemaining = _spawnSequenceBurstDelayRemaining;
            state.SpawnSequenceOriginTransform = _spawnSequenceOriginTransform;
            state.SpawnSequenceFallbackPosition = _spawnSequenceFallbackPosition;
            state.SpawnSequenceFallbackUp = _spawnSequenceFallbackUp;
            state.SpawnSequenceDeltaTime = _spawnSequenceDeltaTime;
            state.RuntimeState = _runtime.CaptureState();

            return state;
        }

        public void ApplyState(SpawnerState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            EnsureSimulationInitialized();
            if (_moduleExecutionCachesDirty)
                RebuildModuleExecutionCaches();

            int previousCount = _bulletCount;
            int sourceCount = state.Bullets?.Length ?? 0;
            _bulletCount = Mathf.Clamp(Mathf.Min(state.BulletCount, sourceCount), 0, _bullets.Length);
            for (int i = 0; i < _bulletCount; i++)
                _bullets[i] = state.Bullets[i];

            int clearCount = Mathf.Max(previousCount, _bulletCount);
            for (int i = _bulletCount; i < clearCount; i++)
            {
                _bullets[i] = default;
                _bulletTransforms[i] = Matrix4x4.zero;
                _bulletColors[i] = float4.zero;
                _bulletTimes[i] = 0f;
            }

            _previousPos = state.PreviousPosition;
            _previousRot = state.PreviousRotation;
            _isStopped = state.IsStopped;
            _hasSpawnedSinceEnable = state.HasSpawnedSinceEnable;
            _fireCooldownRemaining = state.FireCooldownRemaining;
            _nextShotFireRate = state.NextShotFireRate;
            _activeShotFireRate = state.ActiveShotFireRate;
            _spawnSequenceActive = state.SpawnSequenceActive;
            _spawnSequenceIgnoreFireRate = state.SpawnSequenceIgnoreFireRate;
            _spawnSequenceBurstIndex = state.SpawnSequenceBurstIndex;
            _spawnSequenceBurstDelayRemaining = state.SpawnSequenceBurstDelayRemaining;
            _spawnSequenceOriginTransform = state.SpawnSequenceOriginTransform;
            _spawnSequenceFallbackPosition = state.SpawnSequenceFallbackPosition;
            _spawnSequenceFallbackUp = state.SpawnSequenceFallbackUp;
            _spawnSequenceDeltaTime = state.SpawnSequenceDeltaTime;
            _runtime.RestoreState(state.RuntimeState);

            for (int i = 0; i < _bulletCount; i++)
            {
                var bullet = _bullets[i];
                if (bullet.Dead == 1 || bullet.EndOfLife == 1)
                {
                    _bulletTransforms[i] = Matrix4x4.zero;
                    _bulletColors[i] = float4.zero;
                    _bulletTimes[i] = 0f;
                    continue;
                }

                var rot = IsInvalidRotation(bullet.Rotation) ? Quaternion.identity : bullet.Rotation;
                _bulletTransforms[i] = Matrix4x4.TRS(bullet.Position, rot, Vector3.one * bullet.CurrentSize);
                _bulletColors[i] = (Vector4)bullet.Color;
                _bulletTimes[i] = bullet.CurrentLifeSeconds;
            }

            _bulletsFree = true;
            _queuedRenderData = null;
            _spawnPositions.Clear();
            _spawnRotations.Clear();
            _hitDispatchQueue.Clear();
        }

        private void FixedUpdate()
        {
            // Intentionally left empty.
            // Bullet simulation runs in Update to keep color/alpha-over-time smooth under load.
        }

        public T GetModule<T>() where T : IBaseBulletModule
        {
            if (TryGetModule<T>(out var module))
                return module;

            throw new InvalidOperationException($"No module of type {typeof(T).Name} exists on {name}.");
        }

        public bool TryGetModule<T>(out T module) where T : IBaseBulletModule
        {
            for (int i = 0; i < allModules.Count; i++)
            {
                if (allModules[i] is T castModule)
                {
                    module = castModule;
                    return true;
                }
            }

            module = default;
            return false;
        }

        public List<T> GetModulesOfType<T>() where T : IBaseBulletModule
        {
            var modules = new List<T>(allModules.Count);
            GetModulesOfType(modules);
            return modules;
        }

        public void GetModulesOfType<T>(List<T> output) where T : IBaseBulletModule
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();
            for (int i = 0; i < allModules.Count; i++)
            {
                if (allModules[i] is T castModule)
                    output.Add(castModule);
            }
        }

        public void RenderBulletsNow()
        {
            if (_queuedRenderData == null || _disposed) return;

            BulletRenderer.Render(_queuedRenderData.Value.renderData, _bulletTransforms, _bulletColors, _bulletTimes,
                _bulletCount, _queuedRenderData.Value.cam);
        }

        private bool TryGetActiveRenderData(out BulletRenderData activeRenderData)
        {
            activeRenderData = renderData.Data;
            return activeRenderData != null;
        }

        private void Update()
        {
            if (_simulationPaused)
            {
                // Keep render data live while simulation is paused.
                if (_queuedRenderData == null && TryGetActiveRenderData(out var activeRenderData) &&
                    activeRenderData.Texture != null)
                    _queuedRenderData = (activeRenderData, activeRenderData.Camera);
            }
            else
            {
                if (TryGetActiveRenderData(out var activeRenderData))
                    UpdateAllBullets(activeRenderData.Camera, Time.deltaTime);
            }
            if (_queuedRenderData == null || _disposed || _bulletCount == 0) return;
            float priority = -_queuedRenderData.Value.renderData.Priority;
            while (_renderQueue.ContainsKey(priority))
                priority += 0.01f;

            _renderQueue.Add(priority, new RenderQueueData
            {
                Transforms =_bulletTransforms,
                Colors = _bulletColors,
                Times = _bulletTimes,
                Count = _bulletCount,
                RenderData = _queuedRenderData.Value.renderData,
                Camera = _queuedRenderData.Value.cam,
                Spawner = this
            });
        }

        public void UpdateAllBullets(Camera cam, float? dt = null)
        {
            if (!_bullets.IsCreated || _disposed)
                EnsureSimulationInitialized();
            if (!_bulletsFree || this == null) return;
            if (!TryGetActiveRenderData(out var activeRenderData) || activeRenderData.Texture == null) return;
            var deltaTime = Mathf.Max(0f, dt ?? Time.deltaTime);
            _runtime.AdvanceSimulation(deltaTime);

            if (!_spawnSequenceActive)
                _fireCooldownRemaining = Mathf.Max(0f, _fireCooldownRemaining - deltaTime);

            if (Main.FireMode == FireMode.Automatic && enabled && !_isStopped)
                TryStartSpawnSequence(deltaTime, transform, transform.position, transform.up, false, false);

            ProcessActiveSpawnSequence(deltaTime);

            if (_bulletCount == 0)
            {
                _previousPos = transform.position;
                _previousRot = transform.eulerAngles;
                _queuedRenderData = (activeRenderData, cam);
                return;
            }

            _bulletsFree = false;

            var job = new BulletJob
            {
                Active = _enabled,
                Bullets = _bullets,
                ColliderSize = Main.ColliderSize,
                CurrentPosition = transform.position,
                CurrentRotation = transform.eulerAngles,
                DeltaTime = deltaTime,
                MoveWithTransform = Main.MoveWithTransform,
                PreviousPosition = _previousPos,
                PreviousRotation = _previousRot,
                RotateWithTransform = Main.RotateWithTransform,
                UseRotationForDirection = Main.UseRotationForDirection,
                Colors = _bulletColors,
                Times = _bulletTimes,
                Transforms = _bulletTransforms
            };

            var handle = job.Schedule(_bulletCount, 256);
            handle.Complete();
            if (_moduleExecutionCachesDirty)
                RebuildModuleExecutionCaches();
            ExecuteParallelModules(deltaTime);

            for (int i = 0; i < _bulletCount; ++i)
            {
                var bullet = _bullets[i];
                Collider2D collidingObject = null;

                if (bullet.Dead == 0 && bullet.EndOfLife == 0)
                {
                    foreach (var module in _mainThreadBulletModules)
                        module.Execute(ref bullet, deltaTime);

                    HandleCollision(ref bullet, out collidingObject);
                }

                if (bullet.Dead == 1 || bullet.EndOfLife == 1)
                {
                    _bulletTransforms[i] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);
                    _bulletColors[i] = Vector4.zero;
                    _bulletTimes[i] = 0f;
                }
                else
                {
                    var rot = bullet.Rotation;
                    if (IsInvalidRotation(rot))
                        rot = Quaternion.identity;
                    bullet.Rotation = rot;
                    _bulletTransforms[i] = Matrix4x4.TRS(bullet.Position, rot, Vector3.one * bullet.CurrentSize);
                    _bulletColors[i] = (Vector4)bullet.Color;
                    _bulletTimes[i] = bullet.CurrentLifeSeconds;
                }
                
                if (bullet.EndOfLife == 1)
                {
                    if (!ExecuteDieModules(ref bullet, false, null))
                    {
                        bullet.EndOfLife = 0;
                        bullet.Dead = 0;
                    }
                }

                if (bullet.EndOfLife == 1)
                {
                    OnBulletDied?.Invoke(bullet.Id, bullet, true);
                    OnBulletDiedEvent?.Invoke(bullet, true);
                }

                if (bullet is { Dead: 1, EndOfLife: 0 })
                {
                    if (!ExecuteDieModules(ref bullet, true, collidingObject != null ? collidingObject.gameObject : null))
                        bullet.Dead = 0;
                }

                if (bullet is { Dead: 1, EndOfLife: 0 })
                {
                    OnBulletDied?.Invoke(bullet.Id, bullet, false);
                    OnBulletDiedEvent?.Invoke(bullet, false);
                }
                
                _bullets[i] = bullet;
            }
            DispatchQueuedHits();
            
            _bulletsFree = true;

            _previousPos = transform.position;
            _previousRot = transform.eulerAngles;
            
            CompactAliveBullets();
            
            _queuedRenderData = (activeRenderData, cam);

            _handlerCachePruneCounter++;
            if (_handlerCachePruneCounter >= 120)
            {
                PruneHandlerCache();
                _handlerCachePruneCounter = 0;
            }
        }

        private void CompactAliveBullets()
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _bulletCount; readIndex++)
            {
                if (_bullets[readIndex].Dead == 1)
                    continue;

                if (writeIndex != readIndex)
                {
                    _bullets[writeIndex] = _bullets[readIndex];
                    _bulletTransforms[writeIndex] = _bulletTransforms[readIndex];
                    _bulletColors[writeIndex] = _bulletColors[readIndex];
                    _bulletTimes[writeIndex] = _bulletTimes[readIndex];
                }

                writeIndex++;
            }

            for (int i = writeIndex; i < _bulletCount; i++)
                _bulletTransforms[i] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.zero);

            _bulletCount = writeIndex;
        }

        private static bool IsInvalidRotation(Quaternion q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) || q.w == 0;
        }

        private ISpawnerRuntimeModule ResolveRuntimeModule()
        {
            foreach (var module in allModules)
            {
                if (module is not ISpawnerRuntimeModuleProvider runtimeModuleProvider)
                    continue;

                var runtimeModule = runtimeModuleProvider.CreateRuntimeModule();
                if (runtimeModule != null)
                    return runtimeModule;
            }

            return null;
        }

        private void RebuildModuleExecutionCaches()
        {
            _parallelBulletModules.Clear();
            _mainThreadBulletModules.Clear();
            foreach (var module in allModules)
            {
                if (module is IParallelBulletModule parallelModule)
                    _parallelBulletModules.Add(parallelModule);
                else if (module is IBulletModule mainThreadModule)
                    _mainThreadBulletModules.Add(mainThreadModule);
            }
            _runtime.SetRuntimeModule(ResolveRuntimeModule());

            _moduleExecutionCachesDirty = false;
        }

        private void EnsureParallelModuleBufferCapacity(int minLength)
        {
            if (_parallelModuleBuffer.Length >= minLength)
                return;

            int newLength = _parallelModuleBuffer.Length == 0 ? 256 : _parallelModuleBuffer.Length;
            while (newLength < minLength)
                newLength *= 2;
            _parallelModuleBuffer = new BulletContainer[newLength];
        }

        private void ExecuteParallelModules(float deltaTime)
        {
            if (_parallelBulletModules.Count == 0 || _bulletCount == 0)
                return;

            EnsureParallelModuleBufferCapacity(_bulletCount);
            for (int i = 0; i < _bulletCount; i++)
                _parallelModuleBuffer[i] = _bullets[i];

            foreach (var module in _parallelBulletModules)
            {
                Parallel.For(0, _bulletCount, i =>
                {
                    var bullet = _parallelModuleBuffer[i];
                    if (bullet.Dead == 1 || bullet.EndOfLife == 1)
                        return;

                    module.Execute(ref bullet, deltaTime);
                    _parallelModuleBuffer[i] = bullet;
                });
            }

            for (int i = 0; i < _bulletCount; i++)
                _bullets[i] = _parallelModuleBuffer[i];
        }

        private bool ExecuteDieModules(ref BulletContainer bullet, bool isCollision, GameObject collidingObject)
        {
            var shouldDie = true;
            foreach (var module in allModules)
            {
                if (module is not IBulletDieModule dieModule) continue;
                if (dieModule.Execute(ref bullet, isCollision, collidingObject) == IBulletDieModule.CollisionBehaviour.StaysAlive)
                    shouldDie = false;
            }

            return shouldDie;
        }

        private void HandleCollision(ref BulletContainer bullet, out Collider2D collidingObject)
        {
            collidingObject = null;
            var shouldKill = false;
            if (bullet.UseCapsule == 0)
            {
                int numHit =
                    Physics2D.OverlapCircle(new Vector2(bullet.Position.x, bullet.Position.y), bullet.ColliderSize, _filter, _hit);
                if (numHit > 0)
                {
                    Array.Sort(_hit, 0, numHit, ColliderComparer);
                    for (int j = 0; j < numHit; ++j)
                    {
                        var hit = _hit[j];
                        if (!hit.isTrigger)
                        {
                            shouldKill = true;
                            collidingObject ??= hit;
                        }
                        QueueCollisionHit(hit, bullet);
                    }

                    if (shouldKill)
                        bullet.Dead = 1;
                }
            }
            else
            {
                int numHit = Physics2D.OverlapCapsule((Vector3)bullet.Position,
                    new Vector2(bullet.ColliderSize, bullet.CapsuleLength), CapsuleDirection2D.Vertical,
                    bullet.Rotation.eulerAngles.z, _filter, _hit);
                if (numHit > 0)
                {
                    Array.Sort(_hit, 0, numHit, ColliderComparer);
                    for (int j = 0; j < numHit; ++j)
                    {
                        var hit = _hit[j];
                        if (!hit.isTrigger)
                        {
                            shouldKill = true;
                            collidingObject ??= hit;
                        }
                        QueueCollisionHit(hit, bullet);
                    }

                    if (shouldKill)
                        bullet.Dead = 1;
                }
            }
        }
        
        private void QueueCollisionHit(Collider2D hit, BulletContainer bullet)
        {
            if (!Application.isPlaying || hit == null) return;

            var handlers = ResolveHandlers(hit);
            if (handlers.Length == 0) return;

            _hitDispatchQueue.Add(new HitDispatchRecord
            {
                Bullet = bullet,
                Handlers = handlers
            });
        }

        private IBulletHitHandler[] ResolveHandlers(Collider2D hit)
        {
            var id = hit.GetInstanceID();
            if (_handlerCache.TryGetValue(id, out var cachedHandlers))
                return cachedHandlers ?? EmptyHitHandlers;

            var handlers = hit.GetComponentsInChildren<IBulletHitHandler>();
            if (handlers == null || handlers.Length == 0)
                return EmptyHitHandlers;

            Array.Sort(handlers, HandlerComparer);
            _handlerCache[id] = handlers;
            return handlers;
        }

        private void DispatchQueuedHits()
        {
            if (_hitDispatchQueue.Count == 0) return;

            for (int i = 0; i < _hitDispatchQueue.Count; i++)
            {
                var dispatchRecord = _hitDispatchQueue[i];
                var handlers = dispatchRecord.Handlers;
                for (int j = 0; j < handlers.Length; j++)
                {
                    var handler = handlers[j];
                    if (handler == null) continue;
                    handler.Hit(dispatchRecord.Bullet);
                }
            }

            _hitDispatchQueue.Clear();
        }

        private void PruneHandlerCache()
        {
            if (_handlerCache.Count == 0)
                return;

            _handlerCacheKeysToRemove.Clear();
            foreach (var cachedHandlers in _handlerCache)
            {
                var handlers = cachedHandlers.Value;
                if (handlers == null || handlers.Length == 0)
                {
                    _handlerCacheKeysToRemove.Add(cachedHandlers.Key);
                    continue;
                }

                bool allHandlersDead = true;
                for (int i = 0; i < handlers.Length; i++)
                {
                    if (handlers[i] != null)
                    {
                        allHandlersDead = false;
                        break;
                    }
                }

                if (allHandlersDead)
                    _handlerCacheKeysToRemove.Add(cachedHandlers.Key);
            }

            for (int i = 0; i < _handlerCacheKeysToRemove.Count; i++)
                _handlerCache.Remove(_handlerCacheKeysToRemove[i]);
        }

        private void OnDrawGizmosSelected()
        {
            if (_bulletCount == 0 || _disposed) return;
            #if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject != gameObject) return;
            #endif
            var previousColor = Gizmos.color;
            Gizmos.color = Color.green;
            for (int i = _bulletCount - 1; i >= 0; --i)
            {
                if (_bullets[i].Dead == 1) continue;
                var gizmoColliderRadius = Mathf.Max(0f, _bullets[i].ColliderSize);
                if (_bullets[i].UseCapsule == 0)
                    Gizmos.DrawWireSphere(_bullets[i].Position, gizmoColliderRadius);
                else
                {
                    Gizmos.DrawWireSphere(_bullets[i].Position + _bullets[i].Up * _bullets[i].CurrentSize * _bullets[i].CapsuleLength * 0.5f, gizmoColliderRadius);
                    Gizmos.DrawWireSphere(_bullets[i].Position - _bullets[i].Up * _bullets[i].CurrentSize * _bullets[i].CapsuleLength * 0.5f, gizmoColliderRadius);
                }
                //Debug.Log(_bullets[i].Position);
            }
            Gizmos.color = previousColor;
        }

        public bool CheckBulletsRemaining()
        {
            if (burstData.maxActiveBullets != 0)
                return _bulletCount < burstData.maxActiveBullets;
            
            return _bulletCount < MaxBullets;

        }

        public void Spawn(Vector3 position, Vector3 up, float deltaTime)
        {
            TryStartSpawnSequence(deltaTime, null, position, up, false);
        }

        /// <summary>
        /// Spawn immediately, bypassing fire-rate timing checks.
        /// Useful for event-driven spawns such as sub-spawner-on-death.
        /// </summary>
        public void SpawnImmediate(Vector3 position, Vector3 up, float deltaTime)
        {
            TryStartSpawnSequence(deltaTime, null, position, up, true);
        }

        public void Spawn(Transform obj, float deltaTime)
        {
            if (obj == null) return;
            TryStartSpawnSequence(deltaTime, obj, obj.position, obj.up, false);
        }

        private bool TryStartSpawnSequence(
            float deltaTime,
            Transform originTransform,
            Vector3 fallbackPosition,
            Vector3 fallbackUp,
            bool ignoreFireRate,
            bool processImmediately = true)
        {
            if (_spawnSequenceActive || _disposed || !_enabled || !gameObject.activeInHierarchy)
                return false;
            if (!CheckBulletsRemaining())
                return false;
            if (!ignoreFireRate && (_isStopped || _fireCooldownRemaining > 0f))
                return false;

            _spawnSequenceActive = true;
            _spawnSequenceIgnoreFireRate = ignoreFireRate;
            _spawnSequenceBurstIndex = 0;
            _spawnSequenceBurstDelayRemaining = 0f;
            _spawnSequenceOriginTransform = originTransform;
            _spawnSequenceFallbackPosition = fallbackPosition;
            _spawnSequenceFallbackUp = fallbackUp.sqrMagnitude < 0.0001f ? Vector3.up : fallbackUp.normalized;
            _spawnSequenceDeltaTime = Mathf.Max(0f, deltaTime);
            _activeShotFireRate = _nextShotFireRate;
            _hasSpawnedSinceEnable = true;

            OnWeaponFired?.Invoke();
            OnWeaponFiredEvent?.Invoke();

            if (processImmediately && _bulletsFree)
                ProcessActiveSpawnSequence(0f);
            return true;
        }

        private void ProcessActiveSpawnSequence(float deltaTime)
        {
            if (!_spawnSequenceActive)
                return;
            if (!_spawnSequenceIgnoreFireRate && _isStopped)
                return;

            if (_spawnSequenceBurstDelayRemaining > 0f)
            {
                _spawnSequenceBurstDelayRemaining -= deltaTime;
                if (_spawnSequenceBurstDelayRemaining > 0f)
                    return;
                _spawnSequenceBurstDelayRemaining = 0f;
            }

            while (_spawnSequenceActive)
            {
                EmitBurst(_spawnSequenceBurstIndex);
                if (!_spawnSequenceActive)
                    return;

                _spawnSequenceBurstIndex++;
                if (_spawnSequenceBurstIndex >= Mathf.Max(1, burstData.burstCount))
                {
                    CompleteSpawnSequence();
                    return;
                }

                _spawnSequenceBurstDelayRemaining = Mathf.Max(0f, burstData.burstDelay);
                if (_spawnSequenceBurstDelayRemaining > 0f)
                    return;
            }
        }

        private void EmitBurst(int burstNum)
        {
            _spawnPositions.Clear();
            _spawnRotations.Clear();

            int idx = 0;
            spawnShapeData.Spawn((point, dir) =>
            {
                var pos = (Vector3)point;
                var originUp = _spawnSequenceOriginTransform == null
                    ? _spawnSequenceFallbackUp
                    : _spawnSequenceOriginTransform.up;
                var originPosition = _spawnSequenceOriginTransform == null
                    ? _spawnSequenceFallbackPosition
                    : _spawnSequenceOriginTransform.position;
                var extraRotation = Quaternion.LookRotation(Vector3.forward, originUp);

                float moduleDelta = idx == 0 ? _activeShotFireRate : 0f;
                if (!burstData.burstsUpdatePositionEveryBullet)
                    moduleDelta = burstNum == 0 ? moduleDelta : 0f;

                foreach (var module in allModules)
                {
                    if (module is not IBulletSpawnModule spawnModule)
                        continue;
                    spawnModule.Execute(ref pos, ref extraRotation, moduleDelta);
                }

                Vector3 rotatedDir = extraRotation * dir;
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, rotatedDir);
                Vector3 spawnPosition = originPosition + extraRotation * pos;

                _spawnRotations.Add(rotation);
                _spawnPositions.Add(spawnPosition);
                idx++;
            }, _runtime.Random);

            for (int i = 0; i < _spawnPositions.Count; i++)
            {
                if (!CheckBulletsRemaining())
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning($"Tried to spawn too many bullets on manager {name}, didn't spawn one.");
                    #endif
                    CompleteSpawnSequence();
                    return;
                }

                var bulletIndex = _bulletCount;
                var bullet = new BulletContainer
                {
                    Id = bulletIndex,
                    Dead = 0,
                    Position = _spawnPositions[i],
                    Rotation = _spawnRotations[i],
                    Direction = _spawnRotations[i]
                };

                bullet.Damage = _runtime.Sample(main.Damage);
                bullet.Lifetime = _runtime.Sample(main.Lifetime);
                bullet.Speed = _runtime.Sample(main.Speed);
                bullet.CurrentSpeed = bullet.Speed;
                bullet.AngularVelocity = 0f;
                bullet.StartSize = _runtime.Sample(main.StartSize);
                bullet.CurrentSize = bullet.StartSize;
                bullet.StartColor = main.StartColor;
                bullet.Color = bullet.StartColor;
                bullet.ColliderSize = bullet.CurrentSize * main.ColliderSize / 2f;
                bullet.UseCapsule = main.ColliderType == ColliderType.Capsule ? (byte)1 : (byte)0;
                bullet.CapsuleLength = main.CapsuleLength;
                bullet.MovingToOrigin = 0;

                foreach (var module in allModules)
                {
                    if (module is IBulletInitModule initMod)
                        initMod.Execute(ref bullet);
                    if (module is IBulletModule bulletMod)
                        bulletMod.Execute(ref bullet, _spawnSequenceDeltaTime);
                }

                bullet.Speed += burstNum * burstData.stackSpeedIncrease;
                bullet.CurrentSpeed += burstNum * burstData.stackSpeedIncrease;
                _bullets[bulletIndex] = bullet;

                _bulletCount++;
                OnBulletSpawned?.Invoke(bulletIndex, _bullets[bulletIndex]);
                OnBulletSpawnedEvent?.Invoke(bulletIndex, _bullets[bulletIndex]);
            }
        }

        private void CompleteSpawnSequence()
        {
            bool ignoredFireRate = _spawnSequenceIgnoreFireRate;
            _spawnSequenceActive = false;
            _spawnSequenceIgnoreFireRate = false;
            _spawnSequenceBurstIndex = 0;
            _spawnSequenceBurstDelayRemaining = 0f;
            _spawnSequenceOriginTransform = null;
            _spawnSequenceDeltaTime = 0f;

            if (!ignoredFireRate)
            {
                _nextShotFireRate = SampleFireRate();
                _fireCooldownRemaining = _nextShotFireRate;
            }
        }

        /// <summary>
        /// Activate any waiting bullets.
        /// Use this when you want to do bullet tracing.
        /// </summary>
        [ContextMenu("Activate Waiting")]
        public void ActivateWaitingBullets()
        {
            if (!_bullets.IsCreated || _disposed)
                return;

            for (int i = 0; i < _bulletCount; i++)
            {
                var bullet = _bullets[i];
                bullet.Waiting = 0;
                _bullets[i] = bullet;
            }
        }

        private sealed class ColliderInstanceIdComparer : IComparer<Collider2D>
        {
            public int Compare(Collider2D x, Collider2D y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x == null)
                    return 1;
                if (y == null)
                    return -1;
                return x.GetInstanceID().CompareTo(y.GetInstanceID());
            }
        }

        private sealed class BulletHitHandlerInstanceComparer : IComparer<IBulletHitHandler>
        {
            public int Compare(IBulletHitHandler x, IBulletHitHandler y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x == null)
                    return 1;
                if (y == null)
                    return -1;

                var xObject = x as UnityEngine.Object;
                var yObject = y as UnityEngine.Object;
                int xId = xObject != null ? xObject.GetInstanceID() : 0;
                int yId = yObject != null ? yObject.GetInstanceID() : 0;
                return xId.CompareTo(yId);
            }
        }

        private void OnValidate()
        {
            _moduleExecutionCachesDirty = true;
        }
    }
}
