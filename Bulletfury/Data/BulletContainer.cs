using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace BulletFury.Data
{
    /// <summary>
    /// Container for bullet data, to be used for rendering
    /// </summary>
    #if !UNITY_EDITOR
    [BurstCompile]
    #endif
    public struct BulletContainer
    {
        public int Id;
        public float3 Position;
        public float CurrentSize;
        public float ColliderSize;
        public float StartSize;
        public byte Waiting;
        public float TimeToWait;
        public byte Collided;
        public byte UseCapsule;
        public float CapsuleLength;
        public Color Color;
        public Color StartColor;
        public Quaternion Rotation;
        public float3 Forward;
        public float3 Right;
        public float3 Up;
        public float CurrentLifePercent;
        public float CurrentLifeSeconds;
        public byte Dead;
        public byte EndOfLife;
        public float Lifetime;
        public float AngularVelocity;
        public float CurrentSpeed;
        public float3 Velocity;
        public float3 Force;
        public float Damage;
        public Quaternion Direction;
        public byte MovingToOrigin;
        public float3 OriginPosition;
        public float MoveToOriginTime;
        public float MoveToOriginCurrentTime;
        public float3 MoveToOriginStartPosition;
        public float Speed;

        public void InitWithPositionRotationDirection(float3 position, Quaternion rotation, Quaternion direction)
        {
            Position = position;
            Rotation = rotation;
            Direction = direction;
            Dead = 0;
            EndOfLife = 0;
            Waiting = 0;
        }
    }
}