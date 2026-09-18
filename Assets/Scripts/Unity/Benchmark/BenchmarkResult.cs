using System;
using System.Globalization;
using System.Text;

namespace ArenaSurvivor.Unity.Benchmark
{
    /// <summary>
    /// Outcome of one benchmark run. Public fields so <c>JsonUtility</c> can write it to a file.
    /// </summary>
    [Serializable]
    public sealed class BenchmarkResult
    {
        public string appVersion;
        public string date;
        public string device;
        public string gpu;
        public string os;
        public string graphicsApi;
        public int targetFrameRate;
        public float measuredSeconds;
        public int frames;

        public float averageFps;
        public float onePercentLowFps;
        public float averageFrameMs;
        public float p99FrameMs;
        public float maxFrameMs;

        /// <summary>CPU main thread time per frame; 0 if the device does not report frame timings.</summary>
        public float averageCpuMainThreadMs;

        /// <summary>GPU time per frame; 0 if the device does not report frame timings.</summary>
        public float averageGpuMs;

        public int maxAliveEnemies;
        public float averageAliveEnemies;
        public int kills;
        public long allocatedMemoryMB;
        public long gcHeapMB;

        public string ToDisplayText()
        {
            var text = new StringBuilder();
            text.AppendLine($"Average FPS: {F(averageFps)}   (1% low: {F(onePercentLowFps)})");
            text.AppendLine($"Frame time: avg {F(averageFrameMs)} ms, p99 {F(p99FrameMs)} ms, max {F(maxFrameMs)} ms");
            text.AppendLine($"CPU main thread: {Ms(averageCpuMainThreadMs)}   GPU: {Ms(averageGpuMs)}");
            text.AppendLine($"Enemies: max {maxAliveEnemies}, avg {F(averageAliveEnemies)}   Kills: {kills}");
            text.AppendLine($"Memory: {allocatedMemoryMB} MB allocated, GC heap {gcHeapMB} MB");
            text.AppendLine($"{frames} frames over {F(measuredSeconds)} s, cap {targetFrameRate} FPS");
            text.Append($"{device} | {gpu} | {graphicsApi} | v{appVersion}");
            return text.ToString();
        }

        private static string F(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Ms(float value) => value > 0f ? F(value) + " ms" : "n/a";
    }
}
