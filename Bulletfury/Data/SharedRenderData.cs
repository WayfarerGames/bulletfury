using System;
using UnityEngine;

namespace BulletFury
{
    
    [Serializable]
    public struct SharedRenderData
    {
        [SerializeField] private BulletRenderData singleData;
        [SerializeField] private SharedRenderDataSO sharedData;
        public BulletRenderData Data => sharedData == null ? singleData : sharedData;
        
        #if UNITY_EDITOR
        public SharedRenderDataSO SharedDataSO => sharedData;
        #endif
        
        // implicit conversion from SharedVisualData to BulletVisualData
        public static implicit operator BulletRenderData(SharedRenderData sharedData)
        {
            return sharedData.Data;
        }
        
        public static implicit operator SharedRenderData(BulletRenderData data)
        {
            return new SharedRenderData
            {
                singleData = data
            };
        }
        
        public static implicit operator SharedRenderData(SharedRenderDataSO sharedData)
        {
            return new SharedRenderData
            {
                sharedData = sharedData
            };
        }
    }
}