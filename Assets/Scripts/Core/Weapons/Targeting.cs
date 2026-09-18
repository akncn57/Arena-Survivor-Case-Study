using System.Collections.Generic;
using ArenaSurvivor.Core.Enemies;
using UnityEngine;

namespace ArenaSurvivor.Core.Weapons
{
    /// <summary>Target selection for the auto-attacking weapon.</summary>
    public static class Targeting
    {
        /// <summary>
        /// Returns the enemy closest to <paramref name="origin"/> within <paramref name="range"/>, or null.
        /// Distances are measured on the ground plane. Linear scan over all enemies: cheap at the planned
        /// enemy counts, and a candidate for a spatial grid if profiling shows otherwise.
        /// </summary>
        public static Enemy FindNearest(IReadOnlyList<Enemy> enemies, Vector3 origin, float range)
        {
            Enemy nearest = null;
            float nearestSqr = range * range;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                float dx = enemy.Position.x - origin.x;
                float dz = enemy.Position.z - origin.z;
                float distanceSqr = dx * dx + dz * dz;

                if (distanceSqr <= nearestSqr)
                {
                    nearest = enemy;
                    nearestSqr = distanceSqr;
                }
            }

            return nearest;
        }
    }
}
