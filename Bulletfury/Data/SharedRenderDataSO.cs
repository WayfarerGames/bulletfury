using UnityEngine;

namespace BulletFury
{
    [CreateAssetMenu]
    public class SharedRenderDataSO : ScriptableObject
    {
        [SerializeField] private BulletRenderData data;
        
        public void SetData (BulletRenderData data)
        {
            this.data = data;
        }
        
        public static implicit operator BulletRenderData(SharedRenderDataSO sharedData)
        {
            return sharedData.data;
        }
    }
}