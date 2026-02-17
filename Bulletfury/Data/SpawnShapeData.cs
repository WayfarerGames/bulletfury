using System;
using BulletFury.Data;
using Common;
using Common.FloatOrRandom;
using UnityEngine;
using Wayfarer_Games.Common.FloatOrRandom;

namespace BulletFury
{
    [Serializable, Tooltip("Settings for controlling the shape and direction of bullet spawning.")]
    public class SpawnShapeData
    {
        public SpawnDir spawnDir = SpawnDir.Spherised;

        [Min(1), Tooltip("The number of sides in the shape for spawning bullets.")]
        public int numPoints = 4;

        [Min(1), Tooltip("The number of bullets to spawn per side of the shape.")]
        public int numPerSide = 1;
        
        [Min(1), Tooltip("The number of bullets per group, arranged in a circle.")]
        public int numPerGroup = 1;
        
        [Tooltip("The radius of the bullet grouping")]
        public FloatOrRandom groupRadius = 0.25f;

        [Tooltip("Spawn the centre bullet in the group")]
        public bool spawnCentreBullet;
        
        [Tooltip("Make all bullets in the group travel in the same direction")]
        public bool groupDirection;

        [Range(0, 1f), Tooltip("Percentage of bullets to remove from the edge of each side.")]
        public float removeFromEdgePercent = 0f;

        [Tooltip("The radius of the spawn shape.")]
        public FloatOrRandom radius = 1f;

        [Tooltip("The arc of the spawn shape (in degrees).")]
        public FloatOrRandom arc = 360f;
        
        [Tooltip("Randomise the position of the bullet")]
        public bool randomise = false;
        
        [Tooltip("Force the random points to be in a ring")]
        public bool onEdge = false;
        
        [Tooltip("The possible directions for the randomised direction mode")]
        public FloatOrRandom directionArc = 360f;

        public SpawnShapeData()
        {
            spawnDir = SpawnDir.Spherised;
            numPoints = 4;
            numPerSide = 1;
            numPerGroup = 1;
            groupRadius = 0.25f; // Assuming FloatOrRandom is a custom type
            spawnCentreBullet = false; // Default to not spawning center bullet
            groupDirection = false;  // Default to bullets not having the same direction
            removeFromEdgePercent = 0f;
            radius = 1f;
            arc = 360f;
            randomise = false;
            onEdge = false;
            directionArc = 360f;
        }

        private void SpawnGroup(Vector2 position, Vector2 direction, Action<Vector2, Vector2> onGetPoint)
        {
            var offset = 360f / (2 * numPerGroup) - ((0.5f * 360)) + 90f;
            var anglePerSide = 360 / numPerGroup;
            
            for (int i = 0; i < numPerGroup; i++)
            {
                var angle = (i * anglePerSide) + offset;

                angle *= Mathf.Deg2Rad;
                
                Vector2 pos = Quaternion.LookRotation(Vector3.forward, direction) * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * groupRadius;
                
                onGetPoint (pos + position, groupDirection ? direction : (pos).normalized);
            }
        }

        
        private void SpawnLine (Action<Vector2, Vector2> onGetPoint, Squirrel3 rnd)
        {
            Vector2 dir = Vector2.up;
                
            if (spawnDir == SpawnDir.Randomised)
            {
                var rndAngle = rnd.Next() * directionArc * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(rndAngle), Mathf.Sin(rndAngle));
            }

            // for every bullet we should spawn on this side of the shape
            for (int i = 0; i < numPerSide; ++i)
            {
                // position the current point a percentage of the way between each end of the side
                var t = i / (float) numPerSide;
                t += (1f / numPerSide) / 2f;
                var point = Vector2.Lerp(new Vector2(-1, 0), new Vector2(1, 0), t);
                point *= radius;
                    
                // spawn a group of bullets
                
                if (numPerGroup == 1)
                    onGetPoint?.Invoke(point, dir);
                else 
                    SpawnGroup(point, dir, onGetPoint);
                
                if (spawnCentreBullet && numPerGroup > 1)
                    onGetPoint?.Invoke(point, dir);
                    
            }
        }
        
        /// <summary>
        /// Get a point based on the spawning settings
        /// </summary>
        /// <param name="onGetPoint"> a function to run for every point that has been found </param>
        public void Spawn(Action<Vector2, Vector2> onGetPoint, Squirrel3 rnd)
        {
            // initialise the array
            var points = new Vector2[numPoints];
            // take a first pass and add some points to every side

            var offset = arc / (2 * numPoints) - ((0.5f * arc)) + 90f;
            var anglePerSide = arc / numPoints;

            if (numPoints == 1)
            {
                SpawnLine(onGetPoint, rnd);
                return;
            }

            for (int i = 0; i < numPoints; i++)
            {
                var angle = (!randomise ? i * anglePerSide : rnd.Next() * arc) + offset;  

                angle *= Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // set the direction based on the spawnDir enum
                Vector2 dir;
                switch (spawnDir)
                {
                    case SpawnDir.Shape:
                        dir = points[i];
                        break;
                    case SpawnDir.Randomised:
                        dir = Vector2.up;
                        break;
                    case SpawnDir.Spherised:
                        dir = points[i];
                        break;
                    case SpawnDir.Direction:
                        dir = Vector2.up;
                        break;
                    case SpawnDir.Point:
                        dir = points[i];
                        break;
                    default:
                        dir = Vector2.up;
                        break;
                }

                if (spawnDir == SpawnDir.Randomised)
                {
                    var rndAngle = rnd.Next() * directionArc * Mathf.Deg2Rad;
                    dir = new Vector2(Mathf.Cos(rndAngle), Mathf.Sin(rndAngle));
                }
                
                if ((randomise || spawnDir == SpawnDir.Randomised) && !onEdge)
                    points[i] *= rnd.Next();


                if (numPerSide == 1)
                {
                    if (numPerGroup == 1)
                        onGetPoint?.Invoke(points[i] * radius, dir);
                    else 
                        SpawnGroup(points[i] * radius, dir, onGetPoint);
                    
                    
                    if (spawnCentreBullet && numPerGroup > 1)
                        onGetPoint?.Invoke(points[i] * radius, dir);
                }
            }

            if (numPerSide == 1)
                return;

            // for every side
            for (int i = 0; i < numPoints; ++i)
            {
                // get the next position
                var next = i + 1;
                if (next == numPoints)
                    next = 0;

                // the normal of the current side
                var direction = Vector2.Lerp(points[i], points[next], 0.5f).normalized;

                // for every bullet we should spawn on this side of the shape
                for (int j = 0; j < numPerSide; ++j)
                {
                    // position the current point a percentage of the way between each end of the side
                    var t = j / (float) numPerSide;
                    
                    t += (1f / numPerSide) / 2f;

                    if (t > 0.5f - removeFromEdgePercent / 2f && t < 0.5f + removeFromEdgePercent / 2f)
                    {
                        //Debug.Log(t);
                        continue;
                    }
                    var point = Vector2.Lerp(points[i], points[next], t);
                    point *= radius;

                    Vector2 dir;
                    switch (spawnDir)
                    {
                        case SpawnDir.Shape:
                            dir = direction;
                            break;
                        case SpawnDir.Randomised:
                            var rndAngle = rnd.Next() * directionArc * Mathf.Deg2Rad;
                            dir = new Vector2(Mathf.Cos(rndAngle), Mathf.Sin(rndAngle));
                            break;
                        case SpawnDir.Spherised:
                            dir = point.normalized;
                            break;
                        case SpawnDir.Direction:
                            dir = Vector2.up;
                            break;
                        case SpawnDir.Point:
                            dir = t < 0.5f ? points[i] : points[next];
                            break;
                        default:
                            dir = Vector2.up;
                            break;
                    }
                    
                    // tell function what the point and direction is 
                    onGetPoint?.Invoke(point, dir);
                }
            }
        }
    }
}