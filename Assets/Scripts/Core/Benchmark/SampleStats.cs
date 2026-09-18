using System;

namespace ArenaSurvivor.Core.Benchmark
{
    /// <summary>
    /// Collects numeric samples (e.g. frame times in ms) into a preallocated buffer, so recording
    /// during the benchmark allocates nothing. Statistics are computed once at the end.
    /// </summary>
    public sealed class SampleStats
    {
        private readonly float[] _samples;
        private double _sum;

        /// <param name="capacity">Maximum samples kept, e.g. 60 s x 120 FPS = 7200. Extra samples are ignored.</param>
        public SampleStats(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            _samples = new float[capacity];
        }

        public int Count { get; private set; }

        /// <summary>True if samples were dropped because the buffer was full.</summary>
        public bool Overflowed { get; private set; }

        public float Average => Count > 0 ? (float)(_sum / Count) : 0f;

        public float Max { get; private set; }

        public void Add(float value)
        {
            if (Count >= _samples.Length)
            {
                Overflowed = true;
                return;
            }

            _samples[Count++] = value;
            _sum += value;
            if (Count == 1 || value > Max)
            {
                Max = value;
            }
        }

        /// <summary>
        /// Value below which <paramref name="fraction"/> of the samples fall (nearest-rank method),
        /// e.g. 0.99 gives the 99th percentile frame time. Sorts a copy; call once, not every frame.
        /// </summary>
        public float Percentile(float fraction)
        {
            if (Count == 0)
            {
                return 0f;
            }

            fraction = Math.Max(0f, Math.Min(1f, fraction));
            var sorted = new float[Count];
            Array.Copy(_samples, sorted, Count);
            Array.Sort(sorted);

            // The epsilon stops float error from pushing an exact rank up by one
            // (0.99f * 100 is 99.0000019, which would otherwise round up to 100).
            int rank = (int)Math.Ceiling(fraction * Count - 1e-4);
            return sorted[Math.Max(0, rank - 1)];
        }

        public void Clear()
        {
            Count = 0;
            _sum = 0;
            Max = 0f;
            Overflowed = false;
        }
    }
}
