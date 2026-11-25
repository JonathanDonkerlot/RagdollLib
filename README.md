# RagdollLib

A reusable procedural active-ragdoll physics library and MelonLoader mod for **A Township Tale** (PCVR Mono).

Because *A Township Tale* does not ship native ragdoll physics on creatures or character rigs, **RagdollLib** dynamically analyzes skeletal hierarchies at runtime, builds joint-constrained `Rigidbody` + `CapsuleCollider` + `ConfigurableJoint` rigs, cleanly suspends conflicting Animator/AI/Walker controllers, and exposes a simple public C# API (`RagdollLib.API.RagdollAPI`) for third-party modders.

---

## Features

- **Public Modding API (`RagdollLib.API.RagdollAPI`)**:
  - `TriggerRagdoll(Component targetEntity, Vector3 impulseForce, Vector3 worldHitPoint, float durationSeconds = 3.0f)`
  - `TriggerRagdoll(Component targetEntity, Vector3 impulseForce, float durationSeconds = 3.0f)`
  - `RecoverRagdoll(Component targetEntity)`
  - `IsRagdolled(Component targetEntity)` & `GetRagdollState(Component targetEntity)`
  - Lifecycle events: `OnRagdollStarted` and `OnRagdollRecovered`
  - Zero-Harmony weapon/prefab impulse rules via `RegisterImpulseRule(...)`
- **Generic Procedural Rig Builder**:
  - Automatically discovers skeletal bones across *A Township Tale* creature and character rigs (`Spriggull`, `Gotera`, `Turabada`, `Wyrm`, `Babu`, `CharacterDummy`, etc.) with topological fallback for non-humanoid skeletons.
  - Excludes micro-bones and embedded interactive `NetworkEntity` / `Interactor` / `FixedImpactor` nodes.
  - Prevents jitter and terrain tunneling via balanced mass ratios, angular joint limits, `Physics.IgnoreCollision` across all limb and entity colliders, and `ContinuousDynamic` collision detection.
- **Pooling & Despawn Safety**:
  - Hooks `NetworkEntity.OnDestroyedByScene`, `NetworkEntity.OnDestroy`, and `PooledObject.OnReleased` so recycled or unloaded entities never leak detached bones or corrupted physics state.
- **Standalone Built-in Gameplay Toggles**:
  - Includes optional out-of-the-box `RagdollOnDeath` and `RagdollOnHighImpact` preferences in `UserData/MelonPreferences.cfg` (`[RagdollLib]`).

---

## Installation (Players & Server Hosts)

1. Install **MelonLoader v0.7.3+** on *A Township Tale*.
2. Download `RagdollLib.dll` from the [Releases](../../releases) page.
3. Place `RagdollLib.dll` into your `Mods/` folder (or `UserLibs/` if only used as a dependency, though `Mods/` enables automatic lifecycle hooks and configuration).
4. Launch the game or server once to generate `[RagdollLib]` entries in `UserData/MelonPreferences.cfg`.

### Configuration (`UserData/MelonPreferences.cfg`, `[RagdollLib]`)

| Setting | Default | Description |
|---|---|---|
| `RagdollOnDeath` | `true` | Ragdoll creatures automatically when killed (`HealthObject.OnKilled`) |
| `RagdollOnHighImpact` | `true` | Ragdoll entities when single-hit damage meets `HighImpactDamageThreshold` |
| `HighImpactDamageThreshold` | `15.0` | Minimum `HealthDamage` for built-in high-impact ragdolls |
| `HighImpactImpulse` | `180.0` | Base impulse magnitude (N*s) for built-in high-impact ragdolls |
| `DefaultRagdollDuration` | `3.0` | Default seconds before temporary ragdolls recover |
| `ForceMultiplier` | `1.0` | Global impulse multiplier (`0.05` – `10.0`) |
| `AllowPlayerRagdoll` | `false` | Allows triggering ragdolls on `PlayerCharacter` visual rigs |
| `EnableDebugLogging` | `false` | Enables verbose `[RagdollLib]` diagnostics |

---

## Modder Quickstart (Using `RagdollLib.dll`)

### 1. Reference `RagdollLib.dll` in your `.csproj`

```xml
<ItemGroup>
  <Reference Include="RagdollLib">
    <HintPath>..\Mods\RagdollLib.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Ensure your mod loads after `RagdollLib` (optional but recommended if you register rules during `OnInitializeMelon`):

```csharp
[assembly: MelonOptionalDependencies("RagdollLib")]
```

### 2. Trigger a Ragdoll from a Punch or Custom Weapon Hit

```csharp
using RagdollLib.API;
using UnityEngine;

// From an ImpactDetector / Collider / HealthObject hit:
public void OnPunchHit(Component hitTarget, Vector3 punchDirection, Vector3 hitWorldPoint)
{
    Vector3 impulse = (punchDirection.normalized + Vector3.up * 0.35f).normalized * 240f;

    bool started = RagdollAPI.TriggerRagdoll(
        targetEntity: hitTarget,
        impulseForce: impulse,
        worldHitPoint: hitWorldPoint,
        durationSeconds: 3.0f);
}
```

### 3. Trigger a Radial Explosion / Spell Knockback (Center of Mass)

```csharp
using RagdollLib.API;
using UnityEngine;

public void ApplySpellBlast(Component target, Vector3 blastOrigin, float force = 350f)
{
    Vector3 dir = (target.transform.position - blastOrigin).normalized;
    Vector3 impulse = (dir + Vector3.up * 0.45f).normalized * force;

    RagdollAPI.TriggerRagdoll(target, impulse, durationSeconds: 3.5f);
}
```

### 4. Register Custom Weapon / Prefab Impulse Rules (No Harmony Required)

```csharp
using Alta.Impact;
using MelonLoader;
using RagdollLib.API;

public class MyCombatMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Any weapon/tool matching *Warhammer* dealing >= 5 blunt damage ragdolls the target
        RagdollAPI.RegisterImpulseRule(new RagdollImpulseRule
        {
            RuleId = "MyCombatMod.WarhammerLaunch",
            WeaponOrPrefabPattern = "*Warhammer*",
            ImpactTypeFilter = ImpactType.Blunt,
            ImpulseMagnitude = 360f,
            UpwardBoost = 0.35f,
            DurationSeconds = 3.5f,
            MinimumDamage = 5.0f
        });
    }
}
```

### 5. Query State, Force Early Recovery, and Subscribe to Events

```csharp
using RagdollLib.API;
using UnityEngine;

RagdollAPI.OnRagdollStarted += args =>
{
    // args.TargetRoot, args.ImpulseForce, args.WorldHitPoint, args.DurationSeconds, args.IsDeathRagdoll
};

RagdollAPI.OnRagdollRecovered += args =>
{
    // Entity has realigned to ground and resumed AI/animation
};

if (RagdollAPI.IsRagdolled(creature))
{
    RagdollAPI.RecoverRagdoll(creature);
}
```

---

## Building from Source

```powershell
dotnet build RagdollLib.sln -c Release
dotnet test RagdollLib.sln -c Release --no-build
```

The standalone output assembly and IntelliSense XML docs are generated at `RagdollLib/bin/Release/net472/RagdollLib.dll` and `RagdollLib.xml`.

See [ARCHITECTURE.md](ARCHITECTURE.md) for verified game assembly details, joint stability design, and lifecycle state diagrams.
