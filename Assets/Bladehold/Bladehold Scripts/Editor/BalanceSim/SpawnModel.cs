using System;
using System.Collections.Generic;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     Faithful re-implementation of the spawner's type-selection cascade — mirrors
    ///     WaveSpawner.SelectSpawnType / IsEligible / PerWaveBudget (WaveSpawner.cs:405-450).
    ///     Index 0 is the unlimited fallback row; later rows go budget pass → chance rolls, in CSV order.
    /// </summary>
    public class SpawnModel
    {
        public class TypeState
        {
            public SimEnemyType def;
            public int spawnedThisWave;
            public int alive;
        }

        public readonly List<TypeState> types = new List<TypeState>();
        private readonly Random rng;

        public SpawnModel(SimWorld world, Random rng)
        {
            this.rng = rng;
            foreach (SimEnemyType e in world.enemies)
            {
                types.Add(new TypeState { def = e });
            }
        }

        public void BeginWave()
        {
            foreach (TypeState t in types)
            {
                t.spawnedThisWave = 0;
            }
        }

        /// <summary>Mirrors WaveSpawner.SelectSpawnType (WaveSpawner.cs:405-427).</summary>
        public TypeState Select(int currentWave)
        {
            for (int i = 1; i < types.Count; i++)
            {
                TypeState type = types[i];
                if (IsEligible(type, currentWave) && type.spawnedThisWave < PerWaveBudget(type, currentWave))
                {
                    return type;
                }
            }
            for (int i = 1; i < types.Count; i++)
            {
                TypeState type = types[i];
                if (IsEligible(type, currentWave)
                    && type.spawnedThisWave < PerWaveBudget(type, currentWave)
                    && rng.NextDouble() < type.def.spawnChance)
                {
                    return type;
                }
            }
            return types[0];
        }

        /// <summary>Mirrors WaveSpawner.IsEligible (WaveSpawner.cs:429-433).</summary>
        private bool IsEligible(TypeState type, int currentWave)
        {
            return currentWave >= type.def.unlockWave
                && (type.def.maxConcurrent <= 0 || type.alive < type.def.maxConcurrent);
        }

        /// <summary>Mirrors WaveSpawner.PerWaveBudget (WaveSpawner.cs:442-450).</summary>
        private int PerWaveBudget(TypeState type, int currentWave)
        {
            if (type.def.minSpawn <= 0)
            {
                return int.MaxValue;
            }
            int budget = type.def.minSpawn + Math.Max(0, currentWave - type.def.unlockWave);
            return type.def.maxConcurrent > 0 ? Math.Min(budget, type.def.maxConcurrent) : budget;
        }
    }
}
