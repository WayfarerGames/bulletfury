using System.Collections.Generic;
using UnityEngine;

namespace BulletFury
{
    [CreateAssetMenu(menuName = "Bulletfury/Spawner Preset")]
    public class BulletSpawnerPreset : ScriptableObject
    { 
        public bool UseMain;
        public BulletMainData Main;
        public bool UseShape;
        public SpawnShapeData ShapeData;
        public bool UseBurstData;
        public BurstData BurstData;
        public bool UseModules;
        
        [SerializeReference]
        public List<IBaseBulletModule> BulletModules = new ();
    }
}