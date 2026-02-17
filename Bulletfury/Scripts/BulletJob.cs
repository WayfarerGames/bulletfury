using BulletFury.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BulletFury
{
    /// <summary>
    /// A C# job that moves all bullets based on their velocity and current force
    /// </summary>
#if !UNITY_EDITOR
    [BurstCompile]
#endif
    public struct BulletJob : IJobParallelFor
    {
        public NativeArray<BulletContainer> Bullets;
        [WriteOnly] public NativeArray<Matrix4x4> Transforms;
        [WriteOnly] public NativeArray<float4> Colors;
        [WriteOnly] public NativeArray<float> Times;
        
        [ReadOnly] public float DeltaTime;
        [ReadOnly] public bool Active;
        [ReadOnly] public bool UseRotationForDirection;
        [ReadOnly] public bool MoveWithTransform;
        [ReadOnly] public bool RotateWithTransform;
        [ReadOnly] public float ColliderSize;
        [ReadOnly] public float3 CurrentPosition;
        [ReadOnly] public float3 PreviousPosition;
        [ReadOnly] public float3 CurrentRotation;
        [ReadOnly] public float3 PreviousRotation;
        
        public void Execute(int index)
        {
            var bullet = Bullets[index];
            
            Transforms[index] = Matrix4x4.TRS(bullet.Position, bullet.Rotation, Vector3.one * bullet.CurrentSize);
            Times[index] = bullet.CurrentLifeSeconds;
            Colors[index] = (Vector4) bullet.Color;
            
            // Waiting bullets simulate normally until their wait window expires.
            // After that they remain frozen until ActivateWaitingBullets clears Waiting.
            if ((bullet.Waiting == 1 && bullet.CurrentLifeSeconds > bullet.TimeToWait))
                return;

            if (!Active)
                bullet.Dead = 1;
            
            bullet.CurrentLifeSeconds += DeltaTime;
            Times[index] = bullet.CurrentLifeSeconds;
            
            if (bullet.CurrentLifeSeconds > bullet.Lifetime)
            {
                bullet.Dead = 1;
                bullet.EndOfLife = 1;
                Bullets[index] = bullet;
                return;
            }
            
            bullet.ColliderSize = bullet.CurrentSize * ColliderSize / 2f;
            var rot = bullet.Rotation;
            if (IsNaN(rot))
                rot = Quaternion.identity;
            

            if (bullet.MovingToOrigin == 1)
            {
                bullet.MoveToOriginCurrentTime += DeltaTime;
                bullet.Position = math.lerp(bullet.MoveToOriginStartPosition, bullet.OriginPosition,
                    bullet.MoveToOriginCurrentTime / bullet.MoveToOriginTime);
                if (bullet.MoveToOriginCurrentTime >= bullet.MoveToOriginTime)
                    bullet.MovingToOrigin = 0;

                if (bullet.MovingToOrigin == 1)
                {
                    // set the matrix for the current bullet - translation, rotation, scale, in that order.
                    Transforms [index] = Matrix4x4.TRS(bullet.Position,
                        rot,
                        Vector3.one * bullet.CurrentSize);
                    Bullets[index] = bullet;
                    return;
                }
            }

            if (UseRotationForDirection)
                bullet.Velocity = bullet.Rotation * Vector3.up;
            else
                bullet.Velocity = bullet.Direction * Vector3.up;

            bullet.Velocity *= bullet.CurrentSpeed;

            if (MoveWithTransform)
                bullet.Position += CurrentPosition - PreviousPosition;

            if (RotateWithTransform)
            {
                var rotationDelta = quaternion.Euler(CurrentRotation - PreviousRotation);
                bullet.Position = math.mul(rotationDelta, (bullet.Position - CurrentPosition)) +
                                  CurrentPosition;
                bullet.Rotation = rotationDelta * bullet.Rotation;
            }
            
            bullet.CurrentLifePercent = bullet.CurrentLifeSeconds / bullet.Lifetime;
            bullet.Position += bullet.Velocity * DeltaTime +
                               bullet.Force * DeltaTime;


            bullet.Rotation = math.normalize(bullet.Rotation);
            rot = bullet.Rotation;
            if (IsNaN(rot))
                rot = Quaternion.identity;
            
            
            // set the matrix for the current bullet - translation, rotation, scale, in that order.
            Transforms [index] = Matrix4x4.TRS(bullet.Position,
                rot,
                Vector3.one * bullet.CurrentSize);

            bullet.Rotation = rot;
            Bullets[index] = bullet;
        }
        
        private static bool IsNaN(Quaternion q) 
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) || q.w == 0;
        }
    }
}