using System;
using Common.FloatOrRandom;
using UnityEngine;
using Wayfarer_Games.Common.FloatOrRandom;

namespace BulletFury
{
    public enum FireMode { Automatic, Manual }
    public enum ColliderType { Circle, Capsule }
    
    [Serializable]
    public class BulletMainData
    {
        [Tooltip("Automatic: Spawns bullets automatically. Manual: Only spawns bullets when Spawn is called")]
        public FireMode FireMode = FireMode.Automatic;

        [Tooltip("Should the spawner immediately start shooting? Only for Automatic fire mode")]
        public bool PlayOnEnable = true;

        [Tooltip("Seconds between shots spawning")]
        public FloatOrRandom FireRate = 0.1f;

        [Tooltip("How much damage should the bullet deal?")]
        public FloatOrRandom Damage = 1f;

        [Tooltip("How many seconds the bullet stay alive for?")]
        public FloatOrRandom Lifetime = 1f;

        [Tooltip("How many units should the bullet move per second?")]
        public FloatOrRandom Speed = 5f;

        [Tooltip("What colour should the bullets be?"), ColorUsage(true, true)]
        [Header("Visuals")]
        public Color StartColor = Color.white;

        [Tooltip("What size should the bullets be?")]
        public FloatOrRandom StartSize = 1f;

        [Tooltip("Should the bullet use its rotation for its direction?")]
        public bool UseRotationForDirection = true;

        [Tooltip("Should the bullets move with the gameobject?")]
        public bool MoveWithTransform;
        
        [Tooltip("Should the bullets rotate with the gameobject?")]
        public bool RotateWithTransform;

        [Tooltip("What shape should the bullet colliders be? Circle will run much faster!")]
        [Header("Collisions")]
        public ColliderType ColliderType = ColliderType.Circle;
        
        [Range(0, 1), Tooltip("How big should the bullet colliders be as a percentage of bullet size?")]
        public float ColliderSize = 1f;
        
        [Range(0, 1), Tooltip("How long should the collider be?")]
        public float CapsuleLength = 0.1f;
    }
}