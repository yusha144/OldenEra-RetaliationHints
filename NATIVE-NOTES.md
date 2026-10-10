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

`eof.bgdu` invalidates captured previews. Rendering also requires the same hover context and the native target forecast's visible CanvasGroup. In 0.1.2 the widget is a child of the native screen-space cursor UI, with no separate canvas or sorting override; every Graphic has raycasts disabled.

As of 0.1.1, a false native eligibility result clears the current capture and hides the entire annotation before constructing or placing a widget. `Range` is native reaching melee; `Shoot` is the ranged shot. Do not replace native eligibility with an enum-name heuristic.

The first user screenshot exposed unsafe mixed dimensions: border insets were in native local units while the panel size was converted from pixels. Version 0.1.1 normalized those dimensions on the world-space unit bar, but the user then reported that no hint was visible despite a successful placement log. That log does not confirm actual rendering.

Version 0.1.2 resolves `Hex.UI.BattleUIController.me`, requires `ccgc == TypeBattleUI.Normal`, and reads `cszk` (the native `BhCursorTooltips`). `rootCanvasRect` is passed by the game's `BhCursorTooltips.Init` to `BhSimpleTooltip.Init` and serves as the parent coordinate space. We use the normal `simpleTooltip.targetsText.font`, without copying `BhSeeThroughTextUGUI` or its world-space shared material. The screen-space canvas must be active; ScreenSpaceCamera mode requires its real canvas camera. WorldSpace is rejected. The native tooltip object itself need not be visible.

The root normalizes both axes to screen pixels. Text, icon and one-pixel frame all use those coordinates. Placement follows `Input.mousePosition` with a clear gap, flips at screen edges and clamps to the viewport. Invalid coordinates hide the hint. No native transforms or canvases are modified. In addition to attacker, defender and attack identity, changes in destination, counter ability or native damage endpoints invalidate the cached estimate immediately; unchanged calculations retain the 0.12-second throttle.

The user's 0.1.2 screenshot confirms the frame and casualty sprite render, but the label is blank; the corresponding BepInEx log has no calculation exception. The old label used a fresh TMP component with only `font` assigned, fixed 32-pixel row height and `Ellipsis`. The game's tall font line metrics are also documented in the working Skill Synergies renderer. Version 0.1.3 assigns `simpleTooltip.targetsText.fontSharedMaterial` as well as the font (read-only references), uses nonwrapping `Overflow`, forces text mesh preparation and sizes/centers the row from actual visible glyph bounds. The measuring rectangle stays fixed so moving the label does not alter its line layout. Oversized glyph bounds reduce the label's font size to fit the available width and row height. Font assets and materials are never mutated. Content, scale, available width or reactivation invalidate this layout; mere mouse movement does not rebuild it.

Before revealing the row, the mesh must contain visible glyphs with finite positive bounds. The first placement log includes the actual forecast string, glyph count, font and material to distinguish calculation failures from rendering failures. This does not assert that the user has seen the final rendered text. Version 0.1.3 only changes the display and its diagnostics; casualty arithmetic is unchanged. Build passed; live visibility remains for the user.

The damage envelope represents one retaliation under current stats. Luck rolls, triggered damage/buffs/healing, movement-driven aura changes and other units' reactions are deliberately not simulated in 0.1.0. Therefore numeric text has `~`. This is not an exhaustive or guaranteed bound on real combat results. Temporary stacks and overkill protection are explicitly unsupported rather than silently treated as ordinary health.

Local validation covers health arithmetic, ordinary and preemptive ordering, disabled/absent retaliation, lethal opening attacks, invalid forecasts and installed counterattack-pattern multipliers. Version 0.1.2 adds focused pure checks for cursor spacing and viewport edges. Native fields compile against the installed interop; the actual in-game appearance remains for the user's test after restart.
