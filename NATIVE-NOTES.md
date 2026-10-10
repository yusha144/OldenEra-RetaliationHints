# Build-pinned native adapter

These names belong only to Steam build 25672315. Plugin startup checks both GameAssembly.dll and IL2CPP metadata SHA-256 before resolving the hooks.

## Read path

- `eof.bgdy` constructs the game's own DamageUiParams from `cldm` (caster), `cldl` (ability), target and attack-pattern multiplier. It calls `bgdp` (damage forecast) and then `bgdq` (counterattack indicator).
- `eof.bgdy(FieldObject, euk, ESideCastType)` at RVA `0x20F8A40` calls `bgdp` (RVA `0x20F7AC0`) and then `bgdq` (RVA `0x20F7C20`). A reference-only prefix/finalizer brackets this native display call in a thread-local stack. The finalizer only unwinds our scope and does not suppress exceptions.
- `BhBattleUnitInfoView.bghr(evr)` receives the native outgoing damage endpoints. `cmpr` means valid, `cmqa` damaging; `cmps/cmpt` are the HP range. Its postfix records the actual view and range inside the current display scope; no later unit-renderer lookup is needed.
- `eox.bgqn(Unit, Unit, eib, Hex, List<Hex>)` supplies the native counter eligibility result. Capture is restricted to the active display scope plus matching hover caster, ability, target and mouse cell; unrelated AI checks and secondary splash targets are ignored. Native `bgdq` supplies `hover.clcx` as the target hex and `hover.clct` as the path (verified at RVA `0x20F7DAD`).
- `Hex.clbi` / `Hex.fieldObjects` identify occupants. Do not require the mouse hex to equal the defender's `ctvn` origin; check occupancy and retain the captured mouse cell. A context stores its own damage range and panel, rather than pairing independent global caches.
- `eia.bdui` selects the counter ability using the same map/blocker/path inputs as the eligibility check. For melee/range paths, element zero is the attack destination used by this native check.
- `UnitData.ctxr` (native `bijp`) counts full + temporary + wounded-top creatures. `cnbx` is the wounded top HP; `fullStacks` excludes it.
- `evs.bhyd` is the deterministic UI damage factory. It uses current unit stats, ability/dealer modifiers, native damage endpoints (`exu.biop`), caps and casualty conversion; it does not roll battle RNG or execute attacks.
- Attack pattern `charX/charY` addresses the primary target. Only positive multipliers identical across rotations are accepted. Unknown/ambiguous patterns are rejected. Counter patterns in this installation were checked against the catalog.
- `Unit.bigr("counter_before_attack")` reads the active sequence effect from native unit data/buffs.

## Isolation

The adapter uses native `MemberwiseClone` to copy Unit, UnitData, location (`enh`) and the counter ability. It only writes detached health/count/location/owner fields. Other references are read-only; copies are never initialized, placed on the map, added to a side or used in commands/events. Ability damage, RNG, saves and the game's original forecast are never patched.

No Harmony detour accepts `DamageUiParams`: that is a non-blittable IL2CPP value type. The four detours accept only native references (and the primitive eligibility result); the scope hook does not request the enum argument.

## Visibility and approximation

`eof.bgdu` invalidates only the context owned by that hover controller. Rendering also requires the same hover context and the native target forecast's visible CanvasGroup. Since 0.1.2 the widget is a child of the native screen-space cursor UI, with no separate canvas or sorting override; every Graphic has raycasts disabled. The optional simple-tooltip host's own active flag is not a visibility gate; its root canvas and the normal battle UI are.

As of 0.1.1, a false native eligibility result clears the current capture and hides the entire annotation before constructing or placing a widget. `Range` is native reaching melee; `Shoot` is the ranged shot. Do not replace native eligibility with an enum-name heuristic.

The first user screenshot exposed unsafe mixed dimensions: border insets were in native local units while the panel size was converted from pixels. Version 0.1.1 normalized those dimensions on the world-space unit bar, but the user then reported that no hint was visible despite a successful placement log. That log does not confirm actual rendering.

Version 0.1.2 resolves `Hex.UI.BattleUIController.me`, requires `ccgc == TypeBattleUI.Normal`, and reads `cszk` (the native `BhCursorTooltips`). `rootCanvasRect` is passed by the game's `BhCursorTooltips.Init` to `BhSimpleTooltip.Init` and serves as the parent coordinate space. We use the normal `simpleTooltip.targetsText.font`, without copying `BhSeeThroughTextUGUI` or its world-space shared material. The screen-space canvas must be active; ScreenSpaceCamera mode requires its real canvas camera. WorldSpace is rejected. The native tooltip object itself need not be visible.

The root normalizes both axes to screen pixels. Text, icon and one-pixel frame all use those coordinates. Placement follows `Input.mousePosition` with a clear gap, flips at screen edges and clamps to the viewport. Invalid coordinates hide the hint. No native transforms or canvases are modified. In addition to attacker, defender and attack identity, changes in destination, counter ability or native damage endpoints invalidate the cached estimate immediately; unchanged calculations retain the 0.12-second throttle.

The user's 0.1.2 screenshot confirms the frame and casualty sprite render, but the label is blank; the corresponding BepInEx log has no calculation exception. The old label used a fresh TMP component with only `font` assigned, fixed 32-pixel row height and `Ellipsis`. The game's tall font line metrics are also documented in the working Skill Synergies renderer. Version 0.1.3 assigns `simpleTooltip.targetsText.fontSharedMaterial` as well as the font (read-only references), uses nonwrapping `Overflow`, forces text mesh preparation and sizes/centers the row from actual visible glyph bounds. The measuring rectangle stays fixed so moving the label does not alter its line layout. Oversized glyph bounds reduce the label's font size to fit the available width and row height. Font assets and materials are never mutated. Content, scale, available width or reactivation invalidate this layout; mere mouse movement does not rebuild it.

Before revealing the row, the mesh must contain visible glyphs with finite positive bounds. The first placement log includes the actual forecast string, glyph count, font and material to distinguish calculation failures from rendering failures. This does not assert that the user has seen the final rendered text. Version 0.1.3 only changes the display and its diagnostics; casualty arithmetic is unchanged. Build passed; live visibility remains for the user.

The 0.1.3 test again showed no panel. That session logged plugin startup but neither a successful placement nor a display/calculation exception: the exact early guard was not recorded, so the font cannot be claimed as the sole cause. Version 0.1.4 changes capture as described above and adds at most 24 once-per-stage diagnostic lines. They distinguish updater startup, native display entry, damage capture, counter refusal/acceptance, visibility rejection, successful arithmetic and font preparation. An empty first-frame text mesh now leaves the mod root active but transparent and retries on the next frame, instead of repeatedly deactivating the TMP component. Numeric calculation is unchanged. Build and source review passed; no live gameplay verification was performed.

The damage envelope represents one retaliation under current stats. Luck rolls, triggered damage/buffs/healing, movement-driven aura changes and other units' reactions are deliberately not simulated in 0.1.0. Therefore numeric text has `~`. This is not an exhaustive or guaranteed bound on real combat results. Temporary stacks and overkill protection are explicitly unsupported rather than silently treated as ordinary health.

Local validation covers health arithmetic, ordinary and preemptive ordering, disabled/absent retaliation, lethal opening attacks, invalid forecasts and installed counterattack-pattern multipliers. Version 0.1.2 adds focused pure checks for cursor spacing and viewport edges. Native fields compile against the installed interop; the actual in-game appearance remains for the user's test after restart.
