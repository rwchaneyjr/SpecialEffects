# Math Clicker Game — Setup Guide

Click a correct answer to play your blue VFX (particles grow + glow intensifies), then the number and effect disappear. Wrong answers show **Try again**.

## What was added

| Path | Role |
|------|------|
| `Assets/Scripts/MathGameManager.cs` | Equation, spawn 1 correct + 2 decoys, round flow |
| `Assets/Scripts/AnswerChoice.cs` | Float-up motion + click target |
| `Assets/Scripts/MouseAnswerPicker.cs` | Mouse raycast → answer |
| `Assets/Scripts/NumberVisualBuilder.cs` | Builds numbers from digit prefabs **or** TMP |
| `Assets/Scripts/CorrectAnswerVFX.cs` | Grows VFX `size`, brightens `New Color`, then destroys |
| `Assets/Scripts/Editor/MathGameSceneSetup.cs` | Menu: **Window → Math Game → Setup Scene** |

Your VFX Graph (`Assets/New VFX.vfx`) already exposes:

- `size` — particle size (grown on correct answer)
- `New Color` — HDR color used as glow intensity

## Quick start (in Unity)

1. Open this project in **Unity 2022.3**.
2. If prompted, import **TMP Essentials** (Window → TextMeshPro → Import TMP Essential Resources).
3. Open `Assets/Scenes/SampleScene`.
4. Menu: **Window → Math Game → Setup Scene** (also under **Tools** if that menu appears).
5. Press **Play**. Answers appear as TMP numbers until you assign digit prefabs.
6. Click with the mouse:
   - Correct → blue VFX grows/glows → number + VFX vanish → next equation
   - Wrong → “Try again”

## Plug in your number prefabs (from MathShooter2)

1. Export / copy digit prefabs **0–9** into `Assets/Prefabs/` (or any folder under Assets).
2. Select **NumberBuilder** in the Hierarchy.
3. On `NumberVisualBuilder`, fill **Digit Prefabs** array:
   - Element 0 → prefab `0`
   - Element 1 → prefab `1`
   - …
   - Element 9 → prefab `9`
4. Tweak **Digit Spacing** / **Digit Scale** if the 3D models are large or small.

Each answer needs a collider (the scripts add a `BoxCollider` on the answer root). Keep physics enabled so mouse raycasts hit them.

## Correct-answer VFX prefab

**Window → Math Game → Create Correct Answer VFX Prefab** creates:

`Assets/Prefabs/CorrectAnswerVFX.prefab`

It uses `New VFX.vfx` and `CorrectAnswerVFX` to:

1. Play the effect on the correct number  
2. Lerp `size` upward  
3. Multiply HDR `New Color` for a stronger glow  
4. Destroy the number and the VFX instance  

Tune on the prefab / component:

- Start Size / End Size  
- Start Glow / End Glow  
- Grow Duration / Hold After Grow  

## Optional: Particle System instead of VFX Graph

If you use the yellow **confetti** Particle System from MathShooter2, put it on the same GameObject as `CorrectAnswerVFX` (or assign **Fallback Particles**). When no `VisualEffect` is present, the script grows `startSizeMultiplier` instead.

## Controls & flow

- Equation: TMP on the Canvas (`EquationText`)
- Answers: 3 world objects float up from below `SpawnArea`
- Input: left mouse button + physics raycast from Main Camera
- Wrong: feedback TMP shows “Try again” briefly; answers stay so you can click again
- Correct: decoys removed; VFX plays; then next round

## Inspector checklist on GameManager

- Equation Text / Feedback Text  
- Number Builder  
- Spawn Area  
- Correct Vfx Prefab  
- Correct Vfx Asset → `New VFX`  

You can send the number + VFX export anytime; once digit prefabs are assigned, Play Mode uses your 3D numbers automatically.
