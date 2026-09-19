using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArenaSurvivor.Core.Spatial
{
    /// <summary>
    /// Uniform grid over the square arena on the ground plane, for "who is near me" questions.
    /// Each cell holds a linked list of item ids, stored in two int arrays (<c>heads</c> per cell,
    /// <c>next</c> per item), so rebuilding it every frame is O(n) and allocates nothing.
    /// A neighbour query only visits the cells around a position instead of every item.
    /// Positions outside the arena are clamped into the border cells, so they are still found.
    /// </summary>
    public sealed class SpatialGrid
    {
        private const int None = -1;

        private readonly float _halfSize;
        private readonly float _cellSize;
        private readonly int[] _heads;
        private int[] _next;

        /// <param name="halfSize">Half the arena width; the grid covers -halfSize..halfSize on X and Z.</param>
        /// <param name="cellSize">Cell edge length. Should be at least the largest query radius.</param>
        /// <param name="capacity">Initial number of item ids; grows on demand.</param>
        public SpatialGrid(float halfSize, float cellSize, int capacity = 256)
        {
            if (halfSize <= 0f || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Arena size and cell size must be positive.");
            }

            _halfSize = halfSize;
            _cellSize = cellSize;
            CellsPerSide = Math.Max(1, (int)Math.Ceiling(halfSize * 2f / cellSize));
            _heads = new int[CellsPerSide * CellsPerSide];
            _next = new int[Math.Max(1, capacity)];
            Clear();
        }

        public int CellsPerSide { get; }

        /// <summary>Empties every cell. Call before inserting the items of a new frame.</summary>
        public void Clear()
        {
            Array.Fill(_heads, None);
        }

        /// <param name="id">Item id, e.g. its index in a list. Ids must be unique between two <see cref="Clear"/> calls.</param>
        public void Insert(int id, Vector3 position)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Id cannot be negative.");
            }

            if (id >= _next.Length)
            {
                Array.Resize(ref _next, Math.Max(id + 1, _next.Length * 2));
            }

            int cell = CellCoord(position.z) * CellsPerSide + CellCoord(position.x);
            _next[id] = _heads[cell];
            _heads[cell] = id;
        }

        /// <summary>Cell column/row of a coordinate, clamped to the grid.</summary>
        public int CellCoord(float coordinate)
        {
            int c = (int)Math.Floor((coordinate + _halfSize) / _cellSize);
            return Math.Max(0, Math.Min(CellsPerSide - 1, c));
        }

        /// <summary>First item id in a cell, or -1. Walk the rest with <see cref="Next"/>.</summary>
        public int First(int cellX, int cellZ)
        {
            return _heads[cellZ * CellsPerSide + cellX];
        }

        /// <summary>Next item id in the same cell, or -1.</summary>
        public int Next(int id)
        {
            return _next[id];
        }

        /// <summary>
        /// Adds to <paramref name="results"/> every item in the cells overlapping the square of
        /// <paramref name="radius"/> around <paramref name="position"/>. These are candidates:
        /// the caller still checks the real distance. Hot paths walk <see cref="First"/>/<see cref="Next"/>
        /// directly instead, to avoid the list.
        /// </summary>
        public void QueryCandidates(Vector3 position, float radius, List<int> results)
        {
            int minX = CellCoord(position.x - radius), maxX = CellCoord(position.x + radius);
            int minZ = CellCoord(position.z - radius), maxZ = CellCoord(position.z + radius);

            for (int cz = minZ; cz <= maxZ; cz++)
            {
                for (int cx = minX; cx <= maxX; cx++)
                {
                    for (int id = First(cx, cz); id != None; id = Next(id))
                    {
                        results.Add(id);
                    }
                }
            }
        }
    }
}
