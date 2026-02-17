using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BulletFury
{
    [Serializable]
    public class BulletRenderData
    {
        private static Material _animatedMaterial;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int Cols = Shader.PropertyToID("_Cols");
        private static readonly int Rows1 = Shader.PropertyToID("_Rows");
        private static readonly int Frame = Shader.PropertyToID("_Frame");

        public Camera Camera;
        public Texture2D Texture;
        public bool Animated;
        [Min(1)]
        public int Rows = 1, Columns = 1;
        public float PerFrameLength = 0.1f;
        public int Layer;
        public int Priority;
        
        private Material _material = null;
        
        public Material Material
        {
            get
            {
                if (_animatedMaterial == null)
                {
                    _material = null;
                    _animatedMaterial = new Material(Shader.Find("Shader Graphs/AnimatedBullet"))
                    {
                        enableInstancing = true
                    };
                }
                
                if (_material == null)
                {
                    _material = Object.Instantiate(_animatedMaterial);
                    _material.SetTexture(MainTex, Texture);
                    _material.SetInt(Cols, Columns);
                    _material.SetInt(Rows1, Rows);
                    _material.SetFloat(Frame, Mathf.Max(PerFrameLength, 0.01f));
                }
                return _material;
            }
        }

        public static void ResetMaterials()
        {
            if (_animatedMaterial != null)
            {
                DestroyUnityObject(_animatedMaterial);
                _animatedMaterial = null;
            }
        }
        
        // material pool for webgl
        private Queue<Material> materialPool = new Queue<Material>();


        public Material GetMaterial()
        {
            if (materialPool.Count == 0)
            {
                return Object.Instantiate(Material); // Create if pool is empty
            }
            else
            {
                return materialPool.Dequeue(); // Reuse from pool
            }
        }

        public void ReturnMaterial(Material material)
        {
            if (material == null)
                return;
            materialPool.Enqueue(material);
        }

        public void DisposeMaterials()
        {
            if (_material != null)
            {
                DestroyUnityObject(_material);
                _material = null;
            }

            while (materialPool.Count > 0)
            {
                var pooledMaterial = materialPool.Dequeue();
                if (pooledMaterial != null)
                    DestroyUnityObject(pooledMaterial);
            }
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