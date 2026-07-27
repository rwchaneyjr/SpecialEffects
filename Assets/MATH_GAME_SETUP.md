# Math Clicker Game — Setup Guide

Click a correct answer to play your blue VFX (particles grow + glow intensifies), then the number and effect disappear. Wrong answers show **Try again**.

## Play

1. Pull branch `cursor/math-clicker-game-b9bd`
2. Press **Play**
3. On the start menu, pick what to practice:
   - **Addition**
   - **Subtraction**
   - **Times Tables**
   - **Divide**
4. Click **Start Practice**
5. Click answers with the mouse
6. Press **Esc** anytime to pause and change practice modes

Sounds: a soft chime for correct answers, a short low tone for wrong answers (built-in; you can replace the clips on `AnswerSfx`).

## Itch.io / builds — visual effect

Correct answers always show a **blue glowing mesh orb burst** (URP Lit spheres). This works in WebGL and Windows itch builds.

- `Assets/Resources/BlueOrb.mat` — material included in builds
- `Assets/Resources/New VFX.vfx` — also tried on desktop; skipped on WebGL

Rebuild and re-upload to itch after pulling.

## What was added

| Path | Role |
|------|------|
| `Assets/Scripts/MathGameManager.cs` | Equation, spawn 1 correct + 2 decoys, round flow |
| `Assets/Scripts/MathPracticeMenu.cs` | Start/pause menu for operation choice |
| `Assets/Scripts/MathGameBootstrap.cs` | Auto-wires the game when you press Play |
| `Assets/Scripts/AnswerChoice.cs` | Float-up motion + click target |
| `Assets/Scripts/MouseAnswerPicker.cs` | Mouse raycast → answer |
| `Assets/Scripts/NumberVisualBuilder.cs` | Builds numbers from digit prefabs **or** TMP |
| `Assets/Scripts/CorrectAnswerVFX.cs` | Grows VFX `size`, brightens `New Color`, then destroys |
| `Assets/Scripts/Editor/MathGameSceneSetup.cs` | Menu: **Window → Math Game → Setup Scene** |

## Quick start (in Unity)

1. Open this project in **Unity 2022.3**.
2. If prompted, import **TMP Essentials**.
3. Open `Assets/Scenes/SampleScene`.
4. Press **Play** — practice menu appears automatically.
5. Choose operations → **Start Practice** → click answers with the mouse.

## Number prefabs (optional)

Assign digit prefabs **0–9** on `NumberBuilder`. Until then, answers use TMP text.

## Controls

- Start menu: Addition / Subtraction / Times / Divide
- Wrong answer: **Try again**
- Correct: VFX grows + glows, then next round
- **Esc**: pause and reopen the practice menu
