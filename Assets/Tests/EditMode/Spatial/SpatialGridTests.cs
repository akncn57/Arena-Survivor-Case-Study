using System;
using System.Collections.Generic;
using ArenaSurvivor.Core.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace ArenaSurvivor.Tests.EditMode.Spatial
{
    public class SpatialGridTests
    {
        private const float HalfSize = 20f;
        private const float CellSize = 1f;

        private SpatialGrid _grid;
        private List<int> _results;

        [SetUp]
        public void SetUp()
        {
            _grid = new SpatialGrid(HalfSize, CellSize, capacity: 4);
            _results = new List<int>();
        }

        [Test]
        public void CellsCoverTheArena()
        {
            Assert.That(_grid.CellsPerSide, Is.EqualTo(40));
            Assert.That(_grid.CellCoord(-HalfSize), Is.EqualTo(0));
            Assert.That(_grid.CellCoord(HalfSize - 0.01f), Is.EqualTo(39));
        }

        [Test]
        public void Query_FindsItemsInNearbyCells()
        {
            _grid.Insert(0, new Vector3(0.2f, 0f, 0.2f));
            _grid.Insert(1, new Vector3(0.9f, 0f, -0.5f));

            _grid.QueryCandidates(new Vector3(0.5f, 0f, 0f), 1f, _results);

            Assert.That(_results, Is.EquivalentTo(new[] { 0, 1 }));
        }

        [Test]
        public void Query_SkipsFarItems()
        {
            _grid.Insert(0, new Vector3(0f, 0f, 0f));
            _grid.Insert(1, new Vector3(10f, 0f, 10f));

            _grid.QueryCandidates(Vector3.zero, 1f, _results);

            Assert.That(_results, Is.EquivalentTo(new[] { 0 }));
        }

        [Test]
        public void PositionsOutsideArena_AreClampedIntoBorderCells()
        {
            _grid.Insert(0, new Vector3(25f, 0f, -30f));

            _grid.QueryCandidates(new Vector3(19.5f, 0f, -19.5f), 1f, _results);

            Assert.That(_results, Is.EquivalentTo(new[] { 0 }));
        }

        [Test]
        public void Clear_EmptiesAllCells()
        {
            _grid.Insert(0, Vector3.zero);

            _grid.Clear();
            _grid.QueryCandidates(Vector3.zero, 1f, _results);

            Assert.That(_results, Is.Empty);
        }

        [Test]
        public void Insert_BeyondCapacity_Grows()
        {
            for (int i = 0; i < 50; i++)
            {
                _grid.Insert(i, Vector3.zero);
            }

            _grid.QueryCandidates(Vector3.zero, 0.5f, _results);

            Assert.That(_results.Count, Is.EqualTo(50));
        }

        [Test]
        public void FindsExactlyTheSameNeighboursAsBruteForce()
        {
            // 300 random points; every pair closer than the radius must be found through the grid.
            const float radius = 1f;
            var random = new System.Random(5);
            var points = new Vector3[300];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3((float)(random.NextDouble() * 30 - 15), 0f, (float)(random.NextDouble() * 30 - 15));
                _grid.Insert(i, points[i]);
            }

            for (int i = 0; i < points.Length; i++)
            {
                var expected = new HashSet<int>();
                for (int j = 0; j < points.Length; j++)
                {
                    if (j != i && (points[i] - points[j]).sqrMagnitude < radius * radius)
                    {
                        expected.Add(j);
                    }
                }

                _results.Clear();
                _grid.QueryCandidates(points[i], radius, _results);
                var found = new HashSet<int>();
                foreach (int j in _results)
                {
                    if (j != i && (points[i] - points[j]).sqrMagnitude < radius * radius)
                    {
                        found.Add(j);
                    }
                }

                Assert.That(found, Is.EquivalentTo(expected), $"Neighbours of point {i}");
            }
        }

        [Test]
        public void Candidates_AreFarFewerThanBruteForcePairs()
        {
            // 150 enemies spread over a 30 x 30 area: brute force compares every pair (150 x 149 = 22,350),
            // the grid only the items in the 3 x 3 cells around each one.
            var random = new System.Random(8);
            for (int i = 0; i < 150; i++)
            {
                _grid.Insert(i, new Vector3((float)(random.NextDouble() * 30 - 15), 0f, (float)(random.NextDouble() * 30 - 15)));
            }

            int candidates = 0;
            random = new System.Random(8);
            for (int i = 0; i < 150; i++)
            {
                var p = new Vector3((float)(random.NextDouble() * 30 - 15), 0f, (float)(random.NextDouble() * 30 - 15));
                _results.Clear();
                _grid.QueryCandidates(p, 1f, _results);
                candidates += _results.Count;
            }

            TestContext.WriteLine($"Grid candidates: {candidates}, brute-force pairs: {150 * 149}");
            Assert.That(candidates, Is.LessThan(150 * 149 / 10));
        }

        [TestCase(0f, 1f)]
        [TestCase(20f, 0f)]
        public void Constructor_WithInvalidSizes_Throws(float halfSize, float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid(halfSize, cellSize));
        }

        [Test]
        public void Insert_NegativeId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _grid.Insert(-1, Vector3.zero));
        }
    }
}
