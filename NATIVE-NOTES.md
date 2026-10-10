# Build-pinned native adapter

These names belong only to Steam build 25672315. Plugin startup checks both GameAssembly.dll and IL2CPP metadata SHA-256 before resolving the hooks.

## Read path

- `eof.bgdy` constructs the game's own DamageUiParams from `cldm` (caster), `cldl` (ability), target and attack-pattern multiplier. It calls `bgdp` (damage forecast) and then `bgdq` (counterattack indicator).
- `BhBattleUnitInfoView.bghr(evr)` receives the native outgoing damage endpoints. `cmpr` means valid, `cmqa` damaging; `cmps/cmpt` are the HP range. The postfix only stores these values.
- `eox.bgqn(Unit, Unit, eib, Hex, List<Hex>)` supplies the native counter eligibility result. Capture is restricted to the actual current hover caster, ability and target; unrelated AI checks are ignored.
- `eia.bdui` selects the counter ability using the same map/blocker/path inputs as the eligibility check. For melee/range paths, element zero is the attack destination used by this native check.
- `UnitData.ctxr` (native `bijp`) counts full + temporary + wounded-top creatures. `cnbx` is the wounded top HP; `fullStacks` excludes it.
- `evs.bhyd` is the deterministic UI damage factory. It uses current unit stats, ability/dealer modifiers, native damage endpoints (`exu.biop`), caps and casualty conversion; it does not roll battle RNG or execute attacks.
- Attack pattern `charX/charY` addresses the primary target. Only positive multipliers identical across rotations are accepted. Unknown/ambiguous patterns are rejected. Counter patterns in this installation were checked against the catalog.
- `Unit.bigr("counter_before_attack")` reads the active sequence effect from native unit data/buffs.

## Isolation

The adapter uses native `MemberwiseClone` to copy Unit, UnitData, location (`enh`) and the counter ability. It only writes detached health/count/location/owner fields. Other references are read-only; copies are never initialized, placed on the map, added to a side or used in commands/events. Ability damage, RNG, saves and the game's original forecast are never patched.

No Harmony detour accepts `DamageUiParams`: that is a non-blittable IL2CPP value type. The three detours accept only references (and the primitive eligibility result).

## Visibility and approximation

`eof.bgdu` invalidates captured previews. Rendering also requires the same hover context and the native target forecast's visible CanvasGroup. Our widget is a child of the attacker's native bar, with no separate canvas or sorting override; every Graphic has raycasts disabled.

The damage envelope represents one retaliation under current stats. Luck rolls, triggered damage/buffs/healing, movement-driven aura changes and other units' reactions are deliberately not simulated in 0.1.0. Therefore numeric text has `~`. This is not an exhaustive or guaranteed bound on real combat results. Temporary stacks and overkill protection are explicitly unsupported rather than silently treated as ordinary health.

Local validation covers health arithmetic, ordinary and preemptive ordering, disabled/absent retaliation, lethal opening attacks, invalid forecasts and installed counterattack-pattern multipliers. IL2CPP hook execution and actual screen placement await user gameplay testing.
