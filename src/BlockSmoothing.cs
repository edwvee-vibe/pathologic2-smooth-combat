using System.Runtime.CompilerServices;
using Engine.Behaviours.Engines.Controllers;
using Engine.Behaviours.Unity.Mecanim;
using Engine.Common.Components.Parameters;
using Engine.Source.Components.Utilities;
using HarmonyLib;
using UnityEngine;

namespace Pathologic2SmoothCombat
{
    // The game advances the block stance only in the weapon controller's FixedUpdate:
    //   realBlockStance = MoveTowards(realBlockStance, blockStance, fixedDeltaTime / PlayerBlockStanceTime);
    //   animatorState.BlockStance = Smooth12(realBlockStance);
    //   realStamina = MoveTowards(realStamina, lowStamina ? 0 : 1, fixedDeltaTime / PlayerBlockStanceTime);
    //   animatorState.Stamina = realStamina;
    //   playerEnemy.BlockStance = realBlockStance >= 0.5f;   // gameplay: the block protects from here on
    // With PlayerBlockStanceTime = 0.33 s that is ~10 visible steps per raise or lower.
    //
    // The mod keeps its own visual copy of both values, advances it every frame with the same law and speed,
    // and writes it to the animator after the game's per-frame Update (the animator is evaluated after all Updates).
    // realBlockStance / playerEnemy.BlockStance are left to the game, so block timing in combat does not change.
    internal sealed class VisualStance
    {
        public float Block;
        public float Stamina;
        public bool Initialized;
    }

    internal sealed class StanceFields<T> where T : class
    {
        private readonly AccessTools.FieldRef<T, float> blockStance = AccessTools.FieldRefAccess<T, float>("blockStance");
        private readonly AccessTools.FieldRef<T, float> realBlockStance = AccessTools.FieldRefAccess<T, float>("realBlockStance");
        private readonly AccessTools.FieldRef<T, float> realStamina = AccessTools.FieldRefAccess<T, float>("realStamina");
        private readonly AccessTools.FieldRef<T, PlayerAnimatorState> animatorState = AccessTools.FieldRefAccess<T, PlayerAnimatorState>("animatorState");
        private readonly AccessTools.FieldRef<T, IParameter<bool>> lowStamina = AccessTools.FieldRefAccess<T, IParameter<bool>>("lowStamina");
        private readonly ConditionalWeakTable<T, VisualStance> visuals = new ConditionalWeakTable<T, VisualStance>();

        public void Update(T controller)
        {
            VisualStance v = visuals.GetOrCreateValue(controller);
            if (!Plugin.BlockActive)
            {
                // The game's FixedUpdate writes its own values again; start from them when re-enabled.
                v.Initialized = false;
                return;
            }
            PlayerAnimatorState state = animatorState(controller);
            float stanceTime = StanceTime();
            if (state == null || stanceTime <= 0f)
            {
                return;
            }

            float dt = Time.deltaTime;
            float real = realBlockStance(controller);
            float realStam = realStamina(controller);
            IParameter<bool> low = lowStamina(controller);
            float staminaTarget = low != null && low.Value ? 0f : 1f;

            if (!v.Initialized)
            {
                v.Block = real;
                v.Stamina = realStam;
                v.Initialized = true;
            }
            // Normally the visual value is within one physics step of the game's value. A bigger gap means the game
            // snapped it (a punch resets the block to 0, holster, weapon switch): follow the snap.
            float tolerance = (Time.fixedDeltaTime + dt) / stanceTime + 0.001f;
            if (Mathf.Abs(v.Block - real) > tolerance)
            {
                v.Block = real;
            }
            if (Mathf.Abs(v.Stamina - realStam) > tolerance)
            {
                v.Stamina = realStam;
            }

            float step = dt / stanceTime;
            v.Block = Mathf.MoveTowards(v.Block, blockStance(controller), step);
            v.Stamina = Mathf.MoveTowards(v.Stamina, staminaTarget, step);
            state.BlockStance = SmoothUtility.Smooth12(v.Block);
            state.Stamina = v.Stamina;
        }

        private static float StanceTime()
        {
            FightSettingsData settings = ScriptableObjectInstance<FightSettingsData>.Instance;
            return settings != null && settings.Description != null ? settings.Description.PlayerBlockStanceTime : 0f;
        }
    }

    [HarmonyPatch]
    internal static class BlockPatches
    {
        private static readonly StanceFields<PlayerUppercotWeaponControllerBase> Melee = new StanceFields<PlayerUppercotWeaponControllerBase>();
        private static readonly StanceFields<PlayerFlashlightWeaponController> Flashlight = new StanceFields<PlayerFlashlightWeaponController>();

        // PlayerWeaponServiceNew calls Update(IEntity) of the current weapon every frame.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerUppercotWeaponControllerBase), nameof(PlayerUppercotWeaponControllerBase.Update))]
        private static void MeleeUpdate(PlayerUppercotWeaponControllerBase __instance)
        {
            long start = FrameMonitor.Begin();
            Melee.Update(__instance);
            FrameMonitor.End(start);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerFlashlightWeaponController), nameof(PlayerFlashlightWeaponController.Update))]
        private static void FlashlightUpdate(PlayerFlashlightWeaponController __instance)
        {
            long start = FrameMonitor.Begin();
            Flashlight.Update(__instance);
            FrameMonitor.End(start);
        }
    }
}
