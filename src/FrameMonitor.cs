using System.Diagnostics;
using UnityEngine;

namespace Pathologic2SmoothCombat
{
    // Diagnostics for the ragdoll fix: after a ragdoll is switched on, log the longest frame of the next second
    // next to the average frame time before it, plus the time spent in this mod's own code during that second.
    // Comparing these lines with the fix on and off (End key) shows whether the fix adds hitches.
    // Nothing here allocates per frame.
    internal static class FrameMonitor
    {
        private const float WindowSeconds = 1f;

        private static long modTicks;
        private static float averageFrame;
        private static bool windowOpen;
        private static float windowEnd;
        private static float windowMax;
        private static int windowFrames;
        private static float windowBaseline;
        private static long windowModTicksStart;
        private static int windowActivations;
        private static string windowLabel;

        internal static long Begin()
        {
            return Stopwatch.GetTimestamp();
        }

        internal static void End(long start)
        {
            modTicks += Stopwatch.GetTimestamp() - start;
        }

        internal static void RagdollActivated(string name, string kind, int bodies, bool interpolated)
        {
            if (!Plugin.LogRagdollFrameTimes.Value)
            {
                return;
            }
            if (windowOpen)
            {
                windowActivations++;
                return;
            }
            windowOpen = true;
            windowEnd = Time.unscaledTime + WindowSeconds;
            windowMax = 0f;
            windowFrames = 0;
            windowBaseline = averageFrame;
            windowModTicksStart = modTicks;
            windowActivations = 1;
            windowLabel = $"{name} ({kind}, {bodies} bodies, interpolation {(interpolated ? "on" : "off")})";
        }

        /// <summary>Called once per frame from Plugin.Update.</summary>
        internal static void Tick()
        {
            RagdollInterpolation.Tick();
            float dt = Time.unscaledDeltaTime;
            if (!windowOpen)
            {
                averageFrame = averageFrame <= 0f ? dt : averageFrame + (dt - averageFrame) * 0.05f;
                return;
            }
            windowFrames++;
            if (dt > windowMax)
            {
                windowMax = dt;
            }
            if (Time.unscaledTime < windowEnd)
            {
                return;
            }
            windowOpen = false;
            double modMicroseconds = (modTicks - windowModTicksStart) * 1e6 / Stopwatch.Frequency;
            string more = windowActivations > 1 ? $" (+{windowActivations - 1} more in this second)" : "";
            Plugin.Log.LogInfo(
                $"Ragdoll on: {windowLabel}{more}. Next {WindowSeconds:0} s: longest frame {windowMax * 1000f:0.0} ms, " +
                $"average before {windowBaseline * 1000f:0.0} ms, {windowFrames} frames, active ragdolls {RagdollInterpolation.ActiveCount}, " +
                $"mod code {modMicroseconds:0} us");
        }
    }
}
