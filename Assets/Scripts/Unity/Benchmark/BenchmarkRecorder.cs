using System;
using System.IO;
using ArenaSurvivor.Core.Benchmark;
using UnityEngine;
using UnityEngine.Profiling;

namespace ArenaSurvivor.Unity.Benchmark
{
    /// <summary>
    /// Records frame times during a benchmark run and produces a <see cref="BenchmarkResult"/>.
    /// Besides the frame time it reads CPU main thread and GPU times from <see cref="FrameTimingManager"/>,
    /// which works in release builds when "Frame Timing Stats" is enabled in Player Settings.
    /// Recording allocates nothing: all buffers are created up front.
    /// </summary>
    public sealed class BenchmarkRecorder
    {
        // 70 s at 120 FPS is 8400 frames; leaves room for faster devices.
        private const int Capacity = 20000;

        private readonly SampleStats _frameMs = new SampleStats(Capacity);
        private readonly SampleStats _cpuMs = new SampleStats(Capacity);
        private readonly SampleStats _gpuMs = new SampleStats(Capacity);
        private readonly SampleStats _alive = new SampleStats(Capacity);
        private readonly FrameTiming[] _timings = new FrameTiming[1];

        private float _warmupSeconds;
        private float _elapsed;

        public bool IsRecording { get; private set; }

        public void Begin(float warmupSeconds)
        {
            _frameMs.Clear();
            _cpuMs.Clear();
            _gpuMs.Clear();
            _alive.Clear();
            _warmupSeconds = warmupSeconds;
            _elapsed = 0f;
            IsRecording = true;
        }

        /// <param name="unscaledDeltaTime">Real frame time (not clamped by Time.maximumDeltaTime).</param>
        /// <param name="aliveEnemies">Enemies alive this frame.</param>
        public void Tick(float unscaledDeltaTime, int aliveEnemies)
        {
            if (!IsRecording)
            {
                return;
            }

            FrameTimingManager.CaptureFrameTimings();

            _elapsed += unscaledDeltaTime;
            if (_elapsed < _warmupSeconds)
            {
                return;
            }

            _frameMs.Add(unscaledDeltaTime * 1000f);
            _alive.Add(aliveEnemies);

            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                // Values of 0 mean "not reported" on this device; skip them instead of averaging zeros in.
                if (_timings[0].cpuMainThreadFrameTime > 0.0)
                {
                    _cpuMs.Add((float)_timings[0].cpuMainThreadFrameTime);
                }

                if (_timings[0].gpuFrameTime > 0.0)
                {
                    _gpuMs.Add((float)_timings[0].gpuFrameTime);
                }
            }
        }

        public BenchmarkResult Finish(int kills)
        {
            IsRecording = false;

            float p99 = _frameMs.Percentile(0.99f);
            return new BenchmarkResult
            {
                appVersion = Application.version,
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                device = SystemInfo.deviceModel,
                gpu = SystemInfo.graphicsDeviceName,
                os = SystemInfo.operatingSystem,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                targetFrameRate = Application.targetFrameRate,
                measuredSeconds = _frameMs.Average * _frameMs.Count / 1000f,
                frames = _frameMs.Count,
                averageFps = _frameMs.Average > 0f ? 1000f / _frameMs.Average : 0f,
                onePercentLowFps = p99 > 0f ? 1000f / p99 : 0f,
                averageFrameMs = _frameMs.Average,
                p99FrameMs = p99,
                maxFrameMs = _frameMs.Max,
                averageCpuMainThreadMs = _cpuMs.Average,
                averageGpuMs = _gpuMs.Average,
                maxAliveEnemies = (int)_alive.Max,
                averageAliveEnemies = _alive.Average,
                kills = kills,
                allocatedMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024),
                gcHeapMB = GC.GetTotalMemory(false) / (1024 * 1024),
            };
        }

        /// <summary>
        /// Writes the result to persistentDataPath/benchmarks and to the log as a single line
        /// starting with "BENCHMARK_RESULT", so it can be read with <c>adb logcat -s Unity</c>.
        /// </summary>
        public static string Save(BenchmarkResult result)
        {
            string json = JsonUtility.ToJson(result, true);
            Debug.Log("BENCHMARK_RESULT " + JsonUtility.ToJson(result));

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "benchmarks");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(path, json);
                return path;
            }
            catch (IOException e)
            {
                Debug.LogWarning($"Benchmark result could not be written: {e.Message}");
                return null;
            }
        }
    }
}
