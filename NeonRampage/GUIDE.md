# Neon Rampage — Build Guide

A third-person arena action game built only from Unity primitives (cubes, capsules, spheres).
Survive waves of Chasers, Brutes and Shooters, beat a Boss every 5th wave, and win by clearing
wave 10 **or** reaching the target score (10,000 by default).

**Works with:** Unity 2022.3 LTS or Unity 6 (6000.x).
**Input:** classic **Input Manager** (`Input.GetAxis`, `Input.GetKey`). No new Input System needed.
**UI:** built-in uGUI with **Legacy Text** (no TextMeshPro import needed).

| Action | Key |
|---|---|
| Move | W A S D |
| Look | Mouse |
| Jump | Space |
| Sprint | Left Shift (uses stamina) |
| Dash / dodge | Q or Right Mouse (uses stamina, short invincibility) |
| Melee combo (3 hits) | Left Mouse |
| Ranged shot | E or F |
| Pause | Esc |

---

## Part 1 — Unity setup

### 1.1 Create / open the project

The scripts are already in `E:\004 - Unity Hub\NeonRampage\Assets\Scripts`.

**Option A (recommended):** open the folder that is already here.
1. Open **Unity Hub** → **Projects** → **Add** → **Add project from disk**.
2. Select `E:\004 - Unity Hub\NeonRampage` and open it with your installed editor
   (2022.3 LTS or 6000.x). If Hub says the version differs, pick your installed version and click **Continue / Change version**.
3. Unity generates the missing project files on first open (takes a minute or two).

**Option B:** start a fresh project and copy the scripts in.
1. Unity Hub → **New project** → template **3D (Built-In Render Pipeline)** (in 2022 it's just called **3D**).
   Name it e.g. `NeonRampageGame`.
2. Copy the `Assets/Scripts` folder from this folder into the new project's `Assets` folder.

> Either render pipeline works. This guide uses the **Built-in** pipeline. If you use **URP**, use the
> material shader `Universal Render Pipeline/Lit` instead of `Standard`; everything else is identical.

### 1.2 Input settings (classic Input Manager)

1. **Edit → Project Settings → Player → Other Settings → Active Input Handling**.
2. Set it to **Input Manager (Old)** or **Both**. Unity restarts if you change it.
   (Unity 6 templates sometimes default to *Input System Package (New)*, which makes every `Input.GetAxis`
   call throw an error.)
3. **Edit → Project Settings → Input Manager → Axes**: check that these default axes exist (they do in every new
   project): `Horizontal`, `Vertical`, `Jump`, `Mouse X`, `Mouse Y`. Nothing needs to be added; Shift, Q, E, F
   and the mouse buttons are read directly with `KeyCode` / `GetMouseButton`.

### 1.3 Install AI Navigation (needed to bake the NavMesh)

1. **Window → Package Manager**.
2. Top-left dropdown → **Unity Registry**.
3. Find **AI Navigation** (`com.unity.ai.navigation`) → **Install**.

The enemy scripts only use `NavMeshAgent` (built into Unity). The package is just for the **NavMeshSurface**
component used to bake.

### 1.4 Tags and layers

**Edit → Project Settings → Tags and Layers**

- **Tags:** `Player` already exists. Add `Enemy`.
- **Layers** (any free slots, these are suggestions):
  - User Layer 6: `Ground`
  - User Layer 7: `Player`
  - User Layer 8: `Enemy`
  - User Layer 9: `Pickup`

**Edit → Project Settings → Physics → Layer Collision Matrix:** untick **Pickup × Enemy** and
**Pickup × Pickup**, so pickups only react to the player. Leave the rest ticked.

### 1.5 Folders

In the Project window create: `Assets/Materials`, `Assets/Prefabs`, `Assets/Prefabs/Effects`, `Assets/Scenes`.

### 1.6 Neon materials

Create these in `Assets/Materials` (**Right-click → Create → Material**, shader **Standard**).
For the glow, tick **Emission** and set the emission color to the same color (HDR intensity ~1.5).

| Material | Albedo | Emission |
|---|---|---|
| `M_Ground` | very dark blue `#0B0B1A` | off |
| `M_Wall` | dark purple `#1A0D2E` | magenta `#FF00FF` at low intensity (0.3) |
| `M_Player` | cyan `#00FFFF` | cyan |
| `M_Chaser` | orange `#FF6A00` | orange |
| `M_Brute` | red `#FF1A1A` | red |
| `M_Shooter` | purple `#B000FF` | purple |
| `M_Boss` | hot pink `#FF0080` | hot pink, intensity 2 |
| `M_Projectile` | white | white (the script tints player shots cyan and enemy shots pink) |
| `M_PickupHealth` | green `#00FF66` | green |
| `M_PickupDamage` | orange `#FF9900` | orange |
| `M_PickupSpeed` | yellow `#FFFF00` | yellow |
| `M_PickupStamina` | blue `#0099FF` | blue |
| `M_Warning` | red | red, intensity 3 |

### 1.7 Build the arena

1. **File → New Scene → Basic (Built-in)**, save it as `Assets/Scenes/Arena.unity`.
2. Create an **empty** GameObject `Arena` at (0, 0, 0). Everything below goes **inside** it.
3. **Ground:** 3D Object → Cube, name `Ground`, Position (0, -0.5, 0), Scale (40, 1, 40), material `M_Ground`, layer `Ground`.
4. **Walls** (Cubes, material `M_Wall`, layer `Ground`):

   | Name | Position | Scale |
   |---|---|---|
   | Wall_N | (0, 2, 20) | (40, 4, 1) |
   | Wall_S | (0, 2, -20) | (40, 4, 1) |
   | Wall_E | (20, 2, 0) | (1, 4, 40) |
   | Wall_W | (-20, 2, 0) | (1, 4, 40) |

5. **Ramps + platforms** (Cubes, material `M_Wall`, layer `Ground`):

   | Name | Position | Rotation | Scale |
   |---|---|---|---|
   | Platform_A | (12, 1, 12) | (0, 0, 0) | (6, 2, 6) |
   | Ramp_A | (12, 0.9, 6.6) | (-20, 0, 0) | (4, 0.3, 6) |
   | Platform_B | (-12, 1, -12) | (0, 0, 0) | (6, 2, 6) |
   | Ramp_B | (-12, 0.9, -6.6) | (20, 0, 0) | (4, 0.3, 6) |

   Nudge each ramp in the Scene view until its low end touches the ground and its high end meets the platform top.

6. **Obstacles** (Cubes, material `M_Wall`, layer `Ground`): place 4–6 pillars, e.g. Scale (2, 3, 2) at
   (6, 1.5, -6), (-6, 1.5, 6), (0, 1.5, 10), (0, 1.5, -10), (10, 1.5, -2).
7. **Spawn points:** create 4 empty GameObjects (outside `Arena` is fine), named `Spawn_1` … `Spawn_4`, at
   (16, 0, 16), (-16, 0, 16), (16, 0, -16), (-16, 0, -16). Keep them away from obstacles.
8. **Lighting (neon look):** select `Directional Light` → Intensity 0.4, color light blue.
   **Window → Rendering → Lighting → Environment**: Skybox Material = None, Environment Lighting Source = Color,
   Ambient Color dark purple `#1A1030`.
   Select `Main Camera` → Clear Flags / Background Type = **Solid Color**, Background black.

### 1.8 Bake the NavMesh

1. Select `Arena` → **Add Component → NavMeshSurface** (from the AI Navigation package).
2. Settings: **Agent Type** Humanoid, **Collect Objects** = *Current Object Hierarchy*,
   **Use Geometry** = *Physics Colliders*, **Include Layers** = Ground (or Everything).
3. Click **Bake**. A blue overlay appears on the floor, ramps and platform tops.
4. Optional, for smoother ramps: **Window → AI → Navigation → Agents → Humanoid**: Max Slope 45, Step Height 0.4. Then bake again.
5. **Re-bake every time you move walls, ramps or obstacles.**

### 1.9 Manager objects

1. Empty GameObject `GameManager` → Add Component **GameManager** and **AudioSource** (Play On Awake off).
   Drag the AudioSource into the GameManager's *Audio Source* field.
2. Empty GameObject `WaveManager` → Add Component **WaveManager** and **AudioSource** (Play On Awake off).
   Fill its fields after the prefabs exist (Part 3).
3. `Main Camera` → Add Component **CameraFollow** (values in Part 3.1).

---

## Part 2 — The C# scripts

All 10 scripts are full, commented files in `Assets/Scripts/`. Unity compiles them automatically when the project opens.

| File | What it does |
|---|---|
| [PlayerController.cs](Assets/Scripts/PlayerController.cs) | CharacterController movement relative to the camera, sprint, jump, dash with i-frames, health, stamina, speed boost, hurt flash. |
| [PlayerCombat.cs](Assets/Scripts/PlayerCombat.cs) | 3-hit melee combo with sphere hit detection and knockback, ranged shot aimed at screen centre, damage boost, screen shake and hit-stop on the finisher. |
| [CameraFollow.cs](Assets/Scripts/CameraFollow.cs) | Mouse-orbit third-person camera with smooth damping, wall collision and screen shake. |
| [EnemyAI.cs](Assets/Scripts/EnemyAI.cs) | NavMeshAgent AI for Chaser, Brute, Shooter and Boss with Chase / Attack / Retreat states, telegraphed attacks, boss slam + projectile spread, knockback. |
| [EnemyHealth.cs](Assets/Scripts/EnemyHealth.cs) | Enemy HP, white hit flash, floating damage numbers (no prefab needed), hit/death particles, score, random drops. |
| [Projectile.cs](Assets/Scripts/Projectile.cs) | Shared bullet for player and enemies. SphereCast hit detection so it never tunnels through walls. |
| [WaveManager.cs](Assets/Scripts/WaveManager.cs) | Wave loop with countdown breaks, growing enemy counts, type unlocks, boss every 5th wave, per-wave stat scaling, victory after the last wave. |
| [GameManager.cs](Assets/Scripts/GameManager.cs) | Game state (menu / playing / paused / game over / victory), score, Esc pause, restart, quit, hit-stop, effect spawning helper. |
| [UIManager.cs](Assets/Scripts/UIManager.cs) | All screens and the HUD. Buttons are wired in code. Banner messages, damage overlay, cooldown icons. |
| [PickupItem.cs](Assets/Scripts/PickupItem.cs) | Spinning, bobbing pickups: Health, Damage Boost, Speed Boost, Stamina. |

Every tweakable value is a `[SerializeField]` with a tooltip, so you can tune the game from the Inspector.
Every audio field is an optional hook: leave it empty and the game is silent, drag in a clip later to hear it.

---

## Part 3 — Prefabs

General rules:
- Build each prefab in the scene, then drag it from the Hierarchy into `Assets/Prefabs` to make the prefab, then delete it from the scene (except the Player, which stays).
- **Remove the collider from every visual child** (Body, Visor, blade…). Only the root keeps a collider. Extra colliders confuse hit detection.
- Root objects sit with their **pivot at the feet** (y = 0); visuals are offset up by 1.

### 3.0 Effect prefabs (particles)

Create 3 particle prefabs in `Assets/Prefabs/Effects` (**GameObject → Effects → Particle System**, then drag to the folder):

| Setting | `FX_Hit` | `FX_Death` | `FX_Pickup` |
|---|---|---|---|
| Duration | 0.3 | 0.6 | 0.6 |
| Looping | off | off | off |
| Start Lifetime | 0.2–0.4 | 0.4–0.8 | 0.5–0.8 |
| Start Speed | 4–8 | 5–12 | 1–3 |
| Start Size | 0.08–0.15 | 0.15–0.3 | 0.1–0.2 |
| Start Color | white/yellow | orange/pink (gradient) | green |
| Gravity Modifier | 0.5 | 0.3 | -0.5 (floats up) |
| Emission → Rate over Time | 0 | 0 | 0 |
| Emission → Bursts | 1 burst, Count 15 | 1 burst, Count 40 | 1 burst, Count 25 |
| Shape | Sphere, radius 0.1 | Sphere, radius 0.4 | Circle, radius 0.5, rotation X -90 |
| Size over Lifetime | on, curve 1 → 0 | on, curve 1 → 0 | on, curve 1 → 0 |
| Renderer → Material | Default-Particle | Default-Particle | Default-Particle |

Play On Awake must stay **on** (default). The scripts destroy the effect object after it finishes.
(URP: use material `ParticlesUnlit`.)

### 3.1 Main Camera

| Component | Field | Value |
|---|---|---|
| CameraFollow | Target | `Player` |
| | Target Offset | (0, 1.5, 0) |
| | Distance | 6 |
| | Mouse Sensitivity | 3 |
| | Min / Max Pitch | -30 / 60 |
| | Follow Smooth Time | 0.08 |
| | Collision Layers | Default + Ground (NOT Player, NOT Enemy) |
| | Collision Radius | 0.3 |

The camera must **not** be a child of the Player.

### 3.2 Player (stays in the scene, also save as prefab)

1. Create Empty `Player`, Position (0, 0, 0). **Tag = Player**, **Layer = Player**
   (when asked, choose *"No, this object only"* for the layer).
2. Children (each: right-click Player → 3D Object, then **remove its collider component**):

   | Child | Type | Local Position | Scale | Material / notes |
   |---|---|---|---|---|
   | `Body` | Capsule | (0, 1, 0) | (1, 1, 1) | `M_Player` |
   | `Visor` | Cube | (0, 1.5, 0.35) | (0.6, 0.15, 0.3) | `M_Wall` (shows which way you face) |
   | `SwingVisual` | Cube | (0, 1, 1.1) | (1.8, 0.08, 0.4) | `M_Projectile`; leave active, the script hides it |
   | `AttackPoint` | Empty | (0, 1, 1.2) | – | – |
   | `FirePoint` | Empty | (0.3, 1.3, 0.7) | – | – |

3. Components on the `Player` root:

   **Character Controller**
   | Field | Value |
   |---|---|
   | Slope Limit | 45 |
   | Step Offset | 0.3 |
   | Skin Width | 0.08 |
   | Center | (0, 1, 0) |
   | Radius | 0.5 |
   | Height | 2 |

   **PlayerController**
   | Field | Value |
   |---|---|
   | Camera Transform | Main Camera |
   | Walk / Sprint Speed | 6 / 10 |
   | Turn Speed | 15 |
   | Jump Height | 1.6 |
   | Gravity | -25 |
   | Dash Speed / Duration / Cooldown | 24 / 0.18 / 1 |
   | Dash Stamina Cost | 25 |
   | Invincible While Dashing | ✓ |
   | Max Health | 100 |
   | Invincibility After Hit | 0.4 |
   | Max Stamina | 100 |
   | Sprint Stamina Per Second | 20 |
   | Stamina Regen Per Second / Delay | 18 / 0.8 |
   | Body Renderer | `Body` |
   | Dash Effect / Death Effect | `FX_Hit` / `FX_Death` (optional) |
   | Audio Source | the AudioSource below |

   **PlayerCombat**
   | Field | Value |
   |---|---|
   | Combo Damage | size 3: 12, 15, 28 |
   | Combo Knockback | size 3: 3, 4, 9 |
   | Swing Cooldown | 0.3 |
   | Combo Reset Time | 0.9 |
   | Attack Point | `AttackPoint` |
   | Attack Radius | 1.3 |
   | Enemy Layers | Enemy |
   | Swing Visual | `SwingVisual` |
   | Projectile Prefab | `Projectile` (Part 3.4) |
   | Fire Point | `FirePoint` |
   | Projectile Speed / Damage | 30 / 15 |
   | Fire Cooldown | 0.45 |
   | Muzzle Flash Prefab | `FX_Hit` (optional) |
   | Audio Source | the AudioSource below |

   **Audio Source**: Play On Awake off, Spatial Blend 0 (2D).

4. Drag `Player` into `Assets/Prefabs`, keep the one in the scene.

### 3.3 Enemies

**Base enemy (build once, then duplicate):**
1. Create Empty `Chaser`. **Tag = Enemy**, **Layer = Enemy** (*"No, this object only"* is fine, the children have no colliders).
2. Children (remove colliders):
   - `Body`: Capsule, local (0, 1, 0).
   - `Eye`: Cube, local (0, 1.5, 0.4), scale (0.5, 0.15, 0.2), material `M_Warning`.
   - `WindupIndicator`: Sphere, local (0, 2.4, 0), scale (0.4, 0.4, 0.4), material `M_Warning`. Leave active, the script hides it.
   - `FirePoint` (Shooter and Boss only): Empty, local (0, 1.3, 0.7).
3. Root components:
   - **Capsule Collider**: Center (0, 1, 0), Radius 0.5, Height 2.
   - **Nav Mesh Agent**: Agent Type Humanoid, Base Offset 0, Radius 0.5, Height 2, Angular Speed 360, Obstacle Avoidance Quality *Medium*. (Speed, acceleration and stopping distance are set by EnemyAI.)
   - **EnemyHealth** and **EnemyAI** (EnemyAI adds Nav Mesh Agent and EnemyHealth automatically if missing).
   - **Audio Source** (Play On Awake off, Spatial Blend 1) → drag into EnemyAI's *Audio Source*.
4. On **EnemyHealth** for all enemies: Hit Effect `FX_Hit`, Death Effect `FX_Death`, Drop Prefabs = the 4 pickup prefabs (Part 3.5; come back and fill this after making them).

Then make each type (duplicate the base, rename, change material on `Body`, set values):

| Field | **Chaser** | **Brute** | **Shooter** | **Boss** |
|---|---|---|---|---|
| Root Scale | (0.8, 0.8, 0.8) | (1.5, 1.5, 1.5) | (1, 1, 1) | (2.5, 2.5, 2.5) |
| Body material | `M_Chaser` | `M_Brute` | `M_Shooter` | `M_Boss` |
| **EnemyAI** Enemy Type | Chaser | Brute | Shooter | Boss |
| Move Speed | 6.5 | 2.5 | 3.5 | 3 |
| Acceleration | 30 | 10 | 15 | 12 |
| Turn Speed | 12 | 5 | 10 | 4 |
| Attack Range | 1.8 | 2.6 | 14 | 16 |
| Attack Damage | 8 | 25 | 10 | 15 |
| Attack Cooldown | 1.0 | 2.2 | 1.8 | 2.0 |
| Attack Windup | 0.25 | 0.7 | 0.4 | 0.8 |
| Windup Indicator | `WindupIndicator` | `WindupIndicator` | `WindupIndicator` | `WindupIndicator` |
| Retreat After Attack | ✓ | ✗ | ✗ | ✗ |
| Retreat Duration | 0.8 | – | 1.2 | – |
| Too Close Distance | 0 | 0 | 6 | 0 |
| Projectile Prefab | – | – | `Projectile` | `Projectile` |
| Fire Point | – | – | `FirePoint` | `FirePoint` |
| Projectile Speed | – | – | 18 | 16 |
| Line Of Sight Blockers | – | – | Default + Ground | Default + Ground |
| Boss Melee Range / Slam Radius | – | – | – | 4.5 / 5.5 |
| Boss Projectile Count / Spread | – | – | – | 7 / 60 |
| Knockback Resistance | 0 | 0.8 | 0.2 | 0.95 |
| Attack Effect Prefab | `FX_Hit` | `FX_Death` | – | `FX_Death` |
| **EnemyHealth** Max Health | 25 | 120 | 35 | 800 |
| Score Value | 50 | 150 | 100 | 1000 |
| Is Boss | ✗ | ✗ | ✗ | ✓ |
| Drop Chance | 0.2 | 0.4 | 0.3 | 1 (always) |

Drag each into `Assets/Prefabs` (`Chaser`, `Brute`, `Shooter`, `Boss`) and delete them from the scene.

### 3.4 Projectile

1. 3D Object → **Sphere**, name `Projectile`, Scale (0.3, 0.3, 0.3), material `M_Projectile`.
2. **Remove the Sphere Collider** (the script uses a SphereCast instead).
3. Add Component **Projectile**:

   | Field | Value |
   |---|---|
   | Speed / Damage | 25 / 10 (overridden by whoever fires it) |
   | Lifetime | 3 |
   | Radius | 0.15 |
   | Hit Layers | Everything |
   | Knockback | 2 |
   | Player Color / Enemy Color | cyan / pink |
   | Hit Effect Prefab | `FX_Hit` |

4. Optional, looks great: Add Component **Trail Renderer** → Time 0.15, Width 0.15 → 0, material `Default-Particle` (URP: `ParticlesUnlit`).
5. Drag to `Assets/Prefabs`, delete from scene. No Rigidbody needed.

### 3.5 Pickups (4 prefabs)

For each pickup:
1. Create Empty root (name below), **Layer = Pickup**.
2. Child visual (shape below, remove its collider), local position (0, 0, 0).
3. Root components:
   - **Sphere Collider**: **Is Trigger ✓**, Radius 0.8 (generous, easy to grab).
   - **Rigidbody**: **Is Kinematic ✓**, Use Gravity ✗ (needed so trigger events fire reliably).
   - **PickupItem** with the values below; Pickup Effect Prefab `FX_Pickup`.

| Prefab | Visual | Scale | Material | Type | Amount | Boost Duration |
|---|---|---|---|---|---|---|
| `Pickup_Health` | Cube | 0.5 | `M_PickupHealth` | Health | 25 | – |
| `Pickup_Damage` | Sphere | 0.5 | `M_PickupDamage` | DamageBoost | 1.5 (x1.5 dmg) | 8 |
| `Pickup_Speed` | Capsule | 0.4 | `M_PickupSpeed` | SpeedBoost | 1.4 (x1.4 speed) | 6 |
| `Pickup_Stamina` | Cylinder | (0.5, 0.15, 0.5) | `M_PickupStamina` | Stamina | 50 | – |

Shared values: Lifetime 15, Score Value 10, Rotate Speed 90, Bob Height 0.25, Bob Speed 2.

Then open each enemy prefab and fill **EnemyHealth → Drop Prefabs** with these 4.
(Tip: add `Pickup_Health` twice to make health drops more common.)

### 3.6 Fill in the WaveManager

| Field | Value |
|---|---|
| Spawn Points | size 4: `Spawn_1` … `Spawn_4` |
| Chaser / Brute / Shooter / Boss Prefab | the 4 enemy prefabs |
| Total Waves | 10 (0 = endless) |
| Enemies In First Wave | 4 |
| Extra Enemies Per Wave | 2 |
| Boss Every N Waves | 5 |
| Boss Wave Escorts | 3 |
| Spawn Interval | 0.6 |
| Time Between Waves | 5 |
| Max Alive At Once | 15 |
| Brute Start Wave / Shooter Start Wave | 2 / 3 |
| Health / Damage / Speed Increase Per Wave | 0.12 / 0.06 / 0.03 |
| Audio Source | its AudioSource |

**GameManager:** Target Score 10000, Hit Stop Duration 0.04.

---

## Part 4 — UI Canvas hierarchy

In Unity 6 / 2022, text and buttons are under **GameObject → UI → Legacy → Text / Button**.
Sliders are **GameObject → UI → Slider**. Creating the first UI element also creates the **Canvas** and **EventSystem**.

### 4.1 Canvas settings

- **Canvas**: Render Mode *Screen Space – Overlay*.
- **Canvas Scaler**: UI Scale Mode *Scale With Screen Size*, Reference Resolution 1920 × 1080, Match 0.5.
- Add Component **UIManager** to the Canvas.
- **EventSystem**: keep it. If it shows a red message asking to replace the module, click the button so it uses **Standalone Input Module** (old input).

### 4.2 Hierarchy

Order matters: lower in the list = drawn on top.

```
Canvas  (UIManager)
├── DamageOverlay        Image, anchor stretch-all, color red alpha 0, Raycast Target OFF
├── HUD                  Empty RectTransform, anchor stretch-all
│   ├── HealthBar        Slider, top-left, pos (180, -40), size 300×25
│   │                    delete "Handle Slide Area", Interactable OFF, Fill color red
│   ├── StaminaBar       Slider, top-left, pos (180, -75), size 300×15
│   │                    delete "Handle Slide Area", Interactable OFF, Fill color yellow
│   ├── ScoreText        Text, top-right, "SCORE: 0", size 28, align right, cyan
│   ├── WaveText         Text, top-center, "WAVE: 0", size 32, white
│   ├── EnemiesText      Text, top-center below WaveText, "ENEMIES: 0", size 24
│   ├── BreakText        Text, center, pos (0, 150), "NEXT WAVE IN 5", size 36
│   ├── BannerText       Text, center, size 72, bold, magenta, width 1200 height 150
│   ├── Crosshair        Text, center, "+", size 40, white, Raycast Target OFF
│   ├── DashIcon         Image, bottom-left, 64×64, Source Image "Knob",
│   │                    Image Type Filled, Fill Method Radial 360, color cyan
│   └── FireIcon         Image, next to DashIcon, same settings, color pink
├── MainMenuPanel        Image, stretch-all, color black alpha 0.85
│   ├── TitleText        Text "NEON RAMPAGE", size 96, magenta, center top
│   ├── PlayButton       Button (Legacy), child Text "PLAY"
│   └── QuitButton       Button (Legacy), child Text "QUIT"
├── PausePanel           Image, stretch-all, black alpha 0.6
│   ├── TitleText        Text "PAUSED"
│   ├── ResumeButton     Button "RESUME"
│   ├── RestartButton    Button "RESTART"
│   └── MainMenuButton   Button "MAIN MENU"
├── GameOverPanel        Image, stretch-all, dark red alpha 0.85
│   ├── TitleText        Text "GAME OVER"
│   ├── FinalScoreText   Text "FINAL SCORE: 0"
│   ├── RestartButton    Button "RESTART"
│   └── MainMenuButton   Button "MAIN MENU"
└── VictoryPanel         Image, stretch-all, dark cyan alpha 0.85
    ├── TitleText        Text "VICTORY!"
    ├── FinalScoreText   Text "FINAL SCORE: 0"
    ├── RestartButton    Button "PLAY AGAIN"
    └── MainMenuButton   Button "MAIN MENU"
```

Tips:
- For buttons, set the Button's Image color to a dark purple and the child Text to size 32 cyan; stack them with a **Vertical Layout Group** on a child empty if you like.
- Leave all panels **active** in the editor; UIManager shows/hides them at runtime.
- Filled Images only show the *Image Type → Filled* option once a **Source Image** sprite is set (use the built-in `Knob` or `UISprite`).

### 4.3 Linking UIManager fields

| UIManager field | Drag in |
|---|---|
| Player / Player Combat | `Player` (or leave empty, found by tag) |
| Main Menu Panel | `MainMenuPanel` |
| Hud Panel | `HUD` |
| Pause Panel | `PausePanel` |
| Game Over Panel | `GameOverPanel` |
| Victory Panel | `VictoryPanel` |
| Health Bar / Stamina Bar | `HealthBar` / `StaminaBar` |
| Score Text / Wave Text / Enemies Text | `ScoreText` / `WaveText` / `EnemiesText` |
| Break Text / Banner Text | `BreakText` / `BannerText` |
| Banner Duration | 2 |
| Dash Cooldown Image / Fire Cooldown Image | `DashIcon` / `FireIcon` |
| Damage Overlay | `DamageOverlay` |
| Damage Overlay Max Alpha | 0.35 |
| Game Over Score Text | `GameOverPanel/FinalScoreText` |
| Victory Score Text | `VictoryPanel/FinalScoreText` |
| Play Button / Quit Button | `MainMenuPanel/PlayButton` / `QuitButton` |
| Resume Button | `PausePanel/ResumeButton` |
| Pause Restart Button / Pause Main Menu Button | `PausePanel/RestartButton` / `MainMenuButton` |
| Game Over Restart Button / Main Menu Button | `GameOverPanel/RestartButton` / `MainMenuButton` |
| Victory Restart Button / Main Menu Button | `VictoryPanel/RestartButton` / `MainMenuButton` |

You do **not** need to set anything in the buttons' On Click () lists; UIManager connects them in code.

### 4.4 Build settings

**File → Build Settings / Build Profiles → Add Open Scenes** so `Arena` is scene 0 (Restart reloads it).

---

## Part 5 — Testing

### 5.1 How to test checklist

Press **Play** and tick these off:

- [ ] Main menu shows, the world is frozen, the mouse cursor is visible.
- [ ] **Play** hides the menu, shows the HUD, locks the cursor, "NEXT WAVE IN 5" counts down.
- [ ] WASD moves relative to the camera, mouse orbits the camera, the camera can't go through walls.
- [ ] Space jumps; you can walk up the ramps onto the platforms.
- [ ] Holding Shift sprints and drains the yellow bar; it refills after you stop.
- [ ] Q / Right Click dashes; it costs stamina, has a cooldown (DashIcon refills), you take no damage mid-dash.
- [ ] Wave 1 spawns 4 Chasers at the spawn points; they chase you, hit, then back off.
- [ ] Left Click 3 times: three swings, the blade flashes; the 3rd hit knocks enemies back harder with a bigger shake and a tiny freeze.
- [ ] Enemies flash white and show yellow damage numbers when hit.
- [ ] E / F fires a cyan projectile toward the crosshair, with a cooldown (FireIcon).
- [ ] Killed enemies burst into particles, add score, sometimes drop a spinning pickup.
- [ ] Walking into each pickup type applies its effect and shows a banner message.
- [ ] Wave 2 adds Brutes (slow, red sphere warning before a heavy hit). Wave 3 adds Shooters (pink shots, they back away).
- [ ] Getting hit: red screen flash, camera shake, player flashes red, health bar drops.
- [ ] "ENEMIES" counter goes down to 0, "WAVE X CLEARED", then the next countdown.
- [ ] Wave 5 shows "BOSS WAVE 5!"; the boss slams when close and fires a 7-shot spread when far.
- [ ] Esc pauses (world freezes, cursor shows); Resume / Restart / Main Menu all work; Esc again resumes.
- [ ] Health reaching 0 shows Game Over with the final score; Restart starts a fresh game straight away.
- [ ] Reaching the target score or clearing wave 10 shows Victory. (Quick test: set GameManager → Target Score to 300.)
- [ ] Quit stops Play Mode in the editor (and closes the built game).

### 5.2 Common errors and fixes

| Problem | Cause | Fix |
|---|---|---|
| `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package` | Project uses the new Input System only | Project Settings → Player → Active Input Handling = **Input Manager (Old)** or **Both** |
| `"SetDestination" can only be called on an active agent that has been placed on a NavMesh` | NavMesh not baked, or spawn point off the mesh | Bake the NavMeshSurface (1.8); move spawn points onto the blue area |
| Warning `Spawn point 'X' is not near the NavMesh` | Same as above | Same as above |
| Enemies spawn but stand still | No NavMesh, or game paused | Bake; make sure the state is Playing (menu Play pressed) |
| `NavMeshSurface` component not found | AI Navigation package missing | Install it (1.3) |
| `The type or namespace name 'NavMeshAgent' could not be found` | AI built-in module disabled | Package Manager → Built-in → **AI** → Enable |
| Pink / magenta objects | Material shader doesn't match render pipeline | Built-in: use **Standard**. URP: use **Universal Render Pipeline/Lit** (or Edit → Rendering → Materials → Convert) |
| Melee never hits | Enemy Layers mask wrong, or enemy root not on Enemy layer, or no collider on enemy root | Set PlayerCombat → Enemy Layers = Enemy; enemy root layer = Enemy with a Capsule Collider |
| Player's own projectile explodes instantly | A child of the Player still has a collider that isn't tagged Player | Remove colliders from Body / Visor / SwingVisual |
| Enemy projectiles hit the enemy that fired them | Same, on the enemy | Remove colliders from enemy child objects |
| Pickups can't be collected | Collider not a trigger, no kinematic Rigidbody, or Player not tagged | Is Trigger ✓, Rigidbody Is Kinematic ✓, Player tag = Player |
| Buttons don't react to clicks | DamageOverlay blocks raycasts, or no EventSystem | DamageOverlay → Raycast Target off; make sure an EventSystem exists |
| Game frozen when pressing Play | That's the main menu (time is paused) | Click PLAY; check UIManager → Play Button is linked |
| Cursor stuck / invisible after stopping Play Mode | Cursor was locked | Press Esc, or click in the Game view |
| Text is invisible | Text too small for its rect, or color alpha 0 | Increase the RectTransform size; Horizontal/Vertical Overflow = Overflow |
| Filled icon doesn't empty/refill | Image has no Source Image | Assign the `Knob` sprite, Image Type = Filled |
| `NullReferenceException` in UIManager | A field you use isn't linked | Check the table in 4.3 (all fields are optional except what you want to see) |
| `Tag: Player is not defined` / `Enemy` | Tag missing | Add the tags (1.4) |
| Damage numbers don't appear | Show Damage Numbers off, or behind a wall | Tick it on EnemyHealth; numbers render on top of most geometry |
| Camera jitters | Camera is a child of the Player | Unparent it; CameraFollow handles following |
| Player slides off ramps / can't climb | Ramp steeper than Slope Limit | Lower the ramp angle or raise CharacterController Slope Limit |
| Boss gets stuck on walls | Big boss uses the Humanoid NavMesh baked for radius 0.5 | Fine for this arena; for a perfect fit add a "Large" agent type (radius 1.25) and a second NavMeshSurface for it |

---

## Part 6 — Five upgrade ideas

1. **Weapon wheel (new weapons).** Add a `WeaponData` ScriptableObject (damage, fire rate, projectile prefab, spread, pellets) and let PlayerCombat swap between a shotgun (5 pellets, wide spread), a charge cannon (hold E to grow damage and size), and a homing orb (steers toward the nearest `EnemyHealth`). Switch with 1/2/3 or the mouse wheel.
2. **Skill tree between waves.** During the break, pause and show 3 random upgrade cards (+20% melee damage, dash leaves a damaging trail, projectiles pierce, +25 max health, lifesteal on finisher). Store chosen perks in a `PlayerStats` component that PlayerController and PlayerCombat read from.
3. **Animations.** Replace the capsule with a free Mixamo character, add an Animator with Idle/Run/Sprint blend tree driven by speed, three attack clips for the combo, and use **Animation Events** to call the hit check at the exact frame the swing lands instead of instantly.
4. **Better enemy variety and AI.** Add a Bomber (runs in and explodes), a Shielder (blocks frontal damage, must be hit from behind) and a Healer (heals nearby enemies). Give enemies flanking behaviour by picking points around the player instead of running straight at them, and add boss phases at 66% and 33% health.
5. **Juice and progression.** Combo multiplier on score that decays if you stop hitting, slow-motion on the last kill of a wave, Post Processing Bloom for real neon glow (URP Volume), a saved high-score table with `PlayerPrefs`, and an endless mode (Total Waves = 0) with a leaderboard.
