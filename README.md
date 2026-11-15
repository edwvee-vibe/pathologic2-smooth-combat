# Smooth Combat for Pathologic 2

A small [BepInEx](https://github.com/BepInEx/BepInEx) plugin that removes two kinds of ~30 FPS animation from
combat in Pathologic 2.

Pathologic 2 runs its physics at a fixed step of 0.033 s, about 30 times per second. Some animations are tied to that
step and stay at ~30 FPS however high your frame rate is:

- **The player's block.** Raising and lowering the hands into the block stance (fists, knife, scalpel, flashlight)
  moves in about 10 visible steps instead of a smooth motion. The hand pose for low stamina has the same problem.
- **Ragdolls.** NPCs that fall, get knocked down or die move only ~30 times per second while their body is driven by
  physics.

With the mod, both are updated every rendered frame. The fixes only change what is drawn. Combat itself is still
computed by the game as before: block timing (when the block starts protecting you), hits and physics are the same.

The mod also fits with [Smooth NPC Walking](https://github.com/edwvee-vibe/pathologic2-smooth-npc-walking), which fixes
the same ~30 Hz stutter for walking NPCs.

## Requirements

- **Pathologic 2** for Windows. Tested on game version 2.1.31563 (Unity 2022.3).
- **BepInEx 5, 64-bit (x64)**. Pathologic 2 is a 64-bit game, so the x86 (32-bit) build of BepInEx will not load.
  BepInEx 6 is not supported. Tested with BepInEx 5.4.23.

## Installation

1. Install BepInEx 5 x64 if you don't have it yet:
   - Download `BepInEx_win_x64_5.4.x.x.zip` from the
     [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases). Pick the latest 5.4 release, not 6.x.
   - Extract it into the game folder, next to `Pathologic.exe`. For Steam that is usually
     `C:\Program Files (x86)\Steam\steamapps\common\Pathologic\`.
   - Start the game once and quit. BepInEx creates its `BepInEx\plugins` and `BepInEx\config` folders on that first run.
2. Download `Pathologic2SmoothCombat-<version>.zip` from the [Releases](../../releases) page of this repository.
3. Extract it into the game folder. The plugin ends up in
   `BepInEx\plugins\pathologic2-smooth-combat\Pathologic2SmoothCombat.dll`.
4. Start the game. `BepInEx\LogOutput.log` should contain a line starting with `Pathologic 2 Smooth Combat 1.0.0:`.

The mod doesn't modify any game files. To uninstall it, delete the `BepInEx\plugins\pathologic2-smooth-combat`
folder.

## Usage

All fixes are on by default. Press **End** in game to switch between the mod and the vanilla behaviour. A short
message in the top-left corner shows the current mode. The key works while other keys are held, so you can compare
in the middle of a fight.

### Settings

They are stored in `BepInEx\config\pathologic2.smoothcombat.cfg`. The file is created on the first start with the mod.

| Section | Key | Default | Description |
|---|---|---|---|
| General | `Enabled` | `true` | Master switch for all fixes. Toggled in game with `ToggleKey`. |
| General | `ToggleKey` | `End` | Key that toggles `Enabled`. Modifiers are supported, e.g. `End + LeftControl`. |
| General | `ShowMessages` | `true` | Show the on-screen message when toggling. |
| Fixes | `SmoothBlock` | `true` | Smooth block stance and low-stamina hand pose. |
| Fixes | `SmoothRagdolls` | `true` | Interpolate ragdoll bones. |
| Diagnostics | `LogRagdollFrameTimes` | `false` | Log frame times after each ragdoll activation (see below). |

### Checking performance

Smoothing ragdolls costs a little work every frame for each bone of a falling or dead body, about 15 to 20 bones per
body. To check it on your machine, set `LogRagdollFrameTimes = true`. The mod then writes a line to
`BepInEx\LogOutput.log` after each ragdoll activation:

```
Ragdoll on: <NPC> (PuppetMaster, 17 bodies, interpolation on). Next 1 s: longest frame 7.9 ms,
average before 6.8 ms, 144 frames, active ragdolls 1, mod code 35 us
```

Fight with the fix on and off (End key) and compare the "longest frame" values. If you notice a problem, set
`SmoothRagdolls = false` to keep only the block fix.

## How it works

**Block.** The block is not a separate animation but a blend driven by the animator parameter `BlockStance` (0 to 1).
The game advances it only in the weapon controller's `FixedUpdate` (`PlayerUppercotWeaponControllerBase`,
`PlayerFlashlightWeaponController`), by `fixedDeltaTime / PlayerBlockStanceTime` per step. With
`PlayerBlockStanceTime` = 0.33 s that gives about 10 steps. The `Stamina` parameter works the same way. The mod keeps
its own visual copy of both values. A Harmony postfix on the controller's per-frame `Update` advances that copy with
the same law and speed, using `Time.deltaTime`, and writes it to the animator. The animator is evaluated after all
`Update` calls. The game's own value, which decides when the block protects you, is left untouched. When the game
snaps the value (a punch drops the block, a weapon is holstered), the visual copy snaps with it.

**Ragdolls.** Ragdolls are switched on through `Pivot.ActualRagdollWeight`. There are two kinds:

- a PuppetMaster puppet. It is activated at weight 0.03, and its muscles are mapped onto the skeleton every frame;
- ordinary ragdoll bones on the skeleton itself, which become dynamic.

Almost none of the ragdoll bone rigidbodies in the game assets use interpolation (3390 of 3406), so the visible pose
changes only on physics steps. While a ragdoll is on, the mod sets `RigidbodyInterpolation.Interpolate` on its bones,
and it restores the original value when the ragdoll is switched off. Physics is unchanged: Unity just writes an
interpolated position for each moving bone every frame, which shows the body at most one physics step (33 ms) late.

## Building from source

Requirements: the .NET SDK (any recent version; the project targets .NET Framework 4.7.2 through reference
assemblies) and the game with BepInEx 5 installed. The game folder provides the referenced assemblies.

```powershell
# builds dist\Pathologic2SmoothCombat.dll and the release zip dist\Pathologic2SmoothCombat-<version>.zip
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 [-GameDir "D:\Games\Pathologic"]

# copies the DLL into <game>\BepInEx\plugins\pathologic2-smooth-combat\ (-Uninstall removes it)
powershell -NoProfile -ExecutionPolicy Bypass -File install.ps1 [-GameDir "D:\Games\Pathologic"] [-Uninstall]
```

The default game path is `C:\Program Files (x86)\Steam\steamapps\common\Pathologic`.

## About this mod

This mod is vibe-coded. The investigation (decompiling the game code, reading the game assets) and the code were done
by an AI assistant ([Claude Code](https://claude.com/claude-code)) under the direction of
[edwvee](https://github.com/edwvee). Bug reports and pull requests are welcome.

## Changelog

- **1.0.0**: First version: smooth block stance and low-stamina pose, ragdoll interpolation, frame time log.

## License

[MIT](LICENSE). Pathologic 2 is © Ice-Pick Lodge. This project is not affiliated with Ice-Pick Lodge or tinyBuild.
