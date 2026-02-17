using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BulletFury
{
    public static class BulletRenderer
    {
        private static readonly int SortOrder = Shader.PropertyToID("_SortOrder");
        private const int BulletsPerChunk = 400;
        private static Mesh _mesh;
        public static Mesh Mesh => _mesh;
        
        private static Dictionary<BulletRenderData, List<(GraphicsBuffer color, GraphicsBuffer tex, MaterialPropertyBlock props)>> _colorBuffersByChunk;

        private static readonly int InstanceColorBuffer = Shader.PropertyToID("_InstanceColorBuffer");
        private static readonly int InstanceTexBuffer = Shader.PropertyToID("_InstanceTexBuffer");
        private const int UnusedBufferFrameThreshold = 300;

        private static int _newLength;
        private static int _num;
        private static int _frameCounter;
        private static bool _alreadyInitialised;
        private static readonly Matrix4x4[] MatrixChunk = new Matrix4x4[BulletsPerChunk];
        private static readonly Dictionary<BulletRenderData, int> LastRenderedFrameByData = new();

        private static bool _disposed = false;
        private static readonly int Color1 = Shader.PropertyToID("_Color");


#if UNITY_EDITOR
        static BulletRenderer()
        {
            Init();
        }
        #endif
        
        [RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            if (_alreadyInitialised && !_disposed)
                return;

            InitMesh();
            _colorBuffersByChunk = new Dictionary<BulletRenderData, List<(GraphicsBuffer color, GraphicsBuffer tex, MaterialPropertyBlock props)>>();

            _disposed = false;
            _alreadyInitialised = true;

            LateUpdate();
            Application.quitting -= Dispose;
            Application.quitting += Dispose;
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeStateChanged;
            #endif
        }

        #if UNITY_EDITOR
        private static void PlayModeStateChanged(UnityEditor.PlayModeStateChange obj)
        {
            if (obj == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                Dispose();
            if (obj == UnityEditor.PlayModeStateChange.EnteredEditMode)
                Init();
        }
        #endif
        private static async void LateUpdate()
        {
            while (!_disposed)
            {
                if (_disposed)
                {
                    BulletSpawner.RenderQueue.Clear();
                    return;
                }

                foreach (var queue in BulletSpawner.RenderQueue.Values)
                {
                    if (!queue.Spawner.Disposed)
                        Render(queue.RenderData, queue.Transforms, queue.Colors, queue.Times, queue.Count, queue.Camera);
                }
                
                BulletSpawner.RenderQueue.Clear();
                ReleaseUnusedChunkBuffers();
                _frameCounter++;

                await Awaitable.NextFrameAsync();
                
                if (_disposed)
                {
                    BulletSpawner.RenderQueue.Clear();
                    return;
                }
            }
        }
        
        public static void Dispose()
        {
            Application.quitting -= Dispose;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            #endif

            DisposeAllChunkBuffers();
            LastRenderedFrameByData.Clear();
            BulletRenderData.ResetMaterials();

            if (_mesh != null)
            {
                DestroyUnityObject(_mesh);
                _mesh = null;
            }

            _disposed = true;
            BulletSpawner.RenderQueue.Clear();
        }

        public static void Render(BulletRenderData data,
            NativeArray<Matrix4x4> transforms,
            NativeArray<float4> colors,
            NativeArray<float> times,
            int numBullets, 
            Camera cam)
        {
            if (numBullets == 0 || _disposed || _mesh == null) return;
            LastRenderedFrameByData[data] = _frameCounter;
            
            data.Material.SetFloat(SortOrder, data.Priority);

            int chunkCount = Mathf.CeilToInt(numBullets / (float)BulletsPerChunk);
            EnsureChunkBuffers(data, chunkCount);

            _num = 0;
            int chunkIndex = 0;
            while (_num < numBullets)
            {
                _newLength = Mathf.Min(numBullets - _num, BulletsPerChunk);
                
                RenderChunk(data,
                    transforms,
                    colors,
                    times,
                    _num,
                    chunkIndex,
                    _newLength, 
                    cam);
                _num += _newLength;
                chunkIndex++;
            }
        }

        private static void RenderChunk(BulletRenderData data,
            NativeArray<Matrix4x4> transforms,
            NativeArray<float4> colors,
            NativeArray<float> times,
            int startIndex,
            int chunkIndex,
            int length,
            Camera cam)
        {
            var chunkBuffers = _colorBuffersByChunk[data][chunkIndex];
            
            // create a new material property block - this contains the different colours for every instance
            var renderParams = new RenderParams(data.Material)
            {
                layer = data.Layer,
                camera = cam,
                rendererPriority = data.Priority,
                matProps = chunkBuffers.props
            };

            for (int i = 0; i < length; i++)
                MatrixChunk[i] = transforms[startIndex + i];

            chunkBuffers.color.SetData(colors, startIndex, 0, length);
            chunkBuffers.tex.SetData(times, startIndex, 0, length);
            renderParams.matProps.SetBuffer(InstanceColorBuffer, chunkBuffers.color);
            renderParams.matProps.SetBuffer(InstanceTexBuffer, chunkBuffers.tex);
            Graphics.RenderMeshInstanced(renderParams, _mesh, 0, MatrixChunk, length);
        }

        private static void EnsureChunkBuffers(BulletRenderData data, int requiredChunkCount)
        {
            if (!_colorBuffersByChunk.TryGetValue(data, out var buffers))
            {
                buffers = new List<(GraphicsBuffer color, GraphicsBuffer tex, MaterialPropertyBlock props)>(requiredChunkCount);
                _colorBuffersByChunk.Add(data, buffers);
            }

            while (buffers.Count < requiredChunkCount)
            {
                buffers.Add((new GraphicsBuffer(GraphicsBuffer.Target.Structured, BulletsPerChunk, sizeof(float) * 4),
                    new GraphicsBuffer(GraphicsBuffer.Target.Structured, BulletsPerChunk, sizeof(float)),
                    new MaterialPropertyBlock()));
            }
        }

        private static void ReleaseUnusedChunkBuffers()
        {
            if (_colorBuffersByChunk == null || _colorBuffersByChunk.Count == 0)
                return;

            var keysToRelease = new List<BulletRenderData>();
            foreach (var entry in _colorBuffersByChunk)
            {
                var data = entry.Key;
                if (!LastRenderedFrameByData.TryGetValue(data, out var lastRenderedFrame) ||
                    _frameCounter - lastRenderedFrame > UnusedBufferFrameThreshold)
                    keysToRelease.Add(data);
            }

            for (int i = 0; i < keysToRelease.Count; i++)
                ReleaseRenderData(keysToRelease[i]);
        }

        private static void DisposeAllChunkBuffers()
        {
            if (_colorBuffersByChunk == null)
                return;

            foreach (var bufferList in _colorBuffersByChunk.Values)
            {
                for (int i = 0; i < bufferList.Count; i++)
                {
                    bufferList[i].color.Dispose();
                    bufferList[i].tex.Dispose();
                }
            }

            _colorBuffersByChunk.Clear();
        }

        private static void ReleaseRenderData(BulletRenderData data)
        {
            if (_colorBuffersByChunk != null && _colorBuffersByChunk.TryGetValue(data, out var bufferList))
            {
                for (int i = 0; i < bufferList.Count; i++)
                {
                    bufferList[i].color.Dispose();
                    bufferList[i].tex.Dispose();
                }

                _colorBuffersByChunk.Remove(data);
            }

            if (data != null)
                data.DisposeMaterials();

            if (data != null)
                LastRenderedFrameByData.Remove(data);
        }

        private static void InitMesh()
        {
            if (_mesh != null)
                DestroyUnityObject(_mesh);

            var vertices = new Vector3[]
            {
                new(-0.5f, -0.5f),
                new(0.5f, -0.5f),
                new(-0.5f, 0.5f),
                new(0.5f, 0.5f)
            };

            var triangles = new[]
            {
                0, 3, 1,
                3, 0, 2
            };

            var uv = new Vector2[]
            {
                new(0, 0),
                new(1, 0),
                new(0, 1),
                new(1, 1)
            };

            _mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null)
                return;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(obj);
            else
                Object.Destroy(obj);
            #else
            Object.Destroy(obj);
            #endif
        }
    }
}