using System.Collections.Generic;
using Engine.Behaviours.Components;
using HarmonyLib;
using RootMotion.Dynamics;
using UnityEngine;

namespace Pathologic2SmoothCombat
{
    // Ragdolls are switched on through Pivot.ActualRagdollWeight. There are two kinds:
    // - PuppetMaster: a separate rigidbody "puppet" is activated when the weight reaches 0.03 and deactivated
    //   (SetActive(false)) below it. Its LateUpdate maps muscle transforms onto the skeleton every frame;
    // - Ordinary: the skeleton bones themselves are switched from kinematic to dynamic while the weight is > 0.
    // In both cases the bone rigidbodies have no interpolation (3390 of 3406 ragdoll bones in the assets), so their
    // transforms, and the visible body, change only on physics steps (~30 Hz).
    //
    // While a ragdoll is on, the mod sets RigidbodyInterpolation.Interpolate on its bones and restores the original
    // value when it is switched off. Physics itself is unchanged (same steps, same bodies): Unity only writes an
    // interpolated transform for each moving bone every frame. Bones of characters that are not ragdolled are never
    // touched, so kinematic animated bones never get interpolation.
    [HarmonyPatch(typeof(Pivot), nameof(Pivot.ActualRagdollWeight), MethodType.Setter)]
    internal static class RagdollInterpolation
    {
        private const float PuppetThreshold = 0.03f; // Pivot: below this the puppet GameObject is deactivated

        private sealed class Entry
        {
            public Rigidbody[] Bodies;
            public RigidbodyInterpolation[] Original;
            public bool Applied;
        }

        private static readonly AccessTools.FieldRef<Pivot, PuppetMaster> PuppetRef = AccessTools.FieldRefAccess<Pivot, PuppetMaster>("puppet");
        private static readonly Dictionary<Pivot, Entry> Active = new Dictionary<Pivot, Entry>();
        private static readonly List<Pivot> Dead = new List<Pivot>();
        private static float nextCleanup;

        internal static int ActiveCount => Active.Count;

        private static void Prefix(Pivot __instance, out float __state)
        {
            __state = __instance.ActualRagdollWeight;
        }

        private static void Postfix(Pivot __instance, float value, float __state)
        {
            long start = FrameMonitor.Begin();
            bool puppet = __instance.HierarhyStructure == Pivot.HierarhyStructureEnum.PuppetMaster;
            bool wasOn = IsOn(puppet, __state);
            bool isOn = IsOn(puppet, value);
            if (isOn && !wasOn)
            {
                Activate(__instance, puppet);
            }
            else if (!isOn && wasOn)
            {
                Deactivate(__instance);
            }
            FrameMonitor.End(start);
        }

        private static bool IsOn(bool puppet, float weight)
        {
            return puppet ? weight >= PuppetThreshold : weight > 0f;
        }

        private static void Activate(Pivot pivot, bool puppet)
        {
            Rigidbody[] bodies = puppet ? PuppetBodies(PuppetRef(pivot)) : OrdinaryBodies(pivot);
            if (bodies == null || bodies.Length == 0)
            {
                return;
            }
            var entry = new Entry { Bodies = bodies, Original = new RigidbodyInterpolation[bodies.Length] };
            for (int i = 0; i < bodies.Length; i++)
            {
                entry.Original[i] = bodies[i].interpolation;
            }
            Active[pivot] = entry;
            Apply(entry, Plugin.RagdollsActive);
            FrameMonitor.RagdollActivated(pivot.name, puppet ? "PuppetMaster" : "Ordinary", bodies.Length, entry.Applied);
        }

        private static void Deactivate(Pivot pivot)
        {
            if (Active.TryGetValue(pivot, out Entry entry))
            {
                Apply(entry, false);
                Active.Remove(pivot);
            }
        }

        private static Rigidbody[] PuppetBodies(PuppetMaster puppet)
        {
            if (puppet == null || puppet.muscles == null)
            {
                return null;
            }
            var list = new List<Rigidbody>(puppet.muscles.Length);
            foreach (Muscle muscle in puppet.muscles)
            {
                if (muscle == null)
                {
                    continue;
                }
                // Muscle.rigidbody is filled on initiation; on the very first activation it can still be empty.
                Rigidbody body = muscle.rigidbody;
                if (body == null && muscle.joint != null)
                {
                    body = muscle.joint.GetComponent<Rigidbody>();
                }
                if (body != null)
                {
                    list.Add(body);
                }
            }
            return list.ToArray();
        }

        private static Rigidbody[] OrdinaryBodies(Pivot pivot)
        {
            // Pivot has just made the ragdoll bones dynamic (EnableRagdool); the character root rigidbody stays kinematic.
            var list = new List<Rigidbody>();
            foreach (Rigidbody body in pivot.GetComponentsInChildren<Rigidbody>())
            {
                if (!body.isKinematic && body.gameObject != pivot.gameObject)
                {
                    list.Add(body);
                }
            }
            return list.ToArray();
        }

        private static void Apply(Entry entry, bool interpolate)
        {
            for (int i = 0; i < entry.Bodies.Length; i++)
            {
                Rigidbody body = entry.Bodies[i];
                if (body != null)
                {
                    body.interpolation = interpolate ? RigidbodyInterpolation.Interpolate : entry.Original[i];
                }
            }
            entry.Applied = interpolate;
        }

        /// <summary>Re-apply the current setting to ragdolls that are already on (after the toggle key).</summary>
        internal static void ApplyToActive()
        {
            RemoveDestroyed();
            bool interpolate = Plugin.RagdollsActive;
            foreach (Entry entry in Active.Values)
            {
                Apply(entry, interpolate);
            }
        }

        internal static void Tick()
        {
            if (Active.Count > 0 && Time.unscaledTime >= nextCleanup)
            {
                nextCleanup = Time.unscaledTime + 5f;
                RemoveDestroyed();
            }
        }

        private static void RemoveDestroyed()
        {
            Dead.Clear();
            foreach (Pivot pivot in Active.Keys)
            {
                if (pivot == null)
                {
                    Dead.Add(pivot);
                }
            }
            foreach (Pivot pivot in Dead)
            {
                Active.Remove(pivot);
            }
            Dead.Clear();
        }
    }
}
