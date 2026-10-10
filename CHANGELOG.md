# Changes

## 0.1.5 — 2026-10-10

- Replace the cursor label with the user's choice: «Потери». Retain «Потери до атаки» for preemptive retaliation and an explicit unavailable state.
- Capture, calculation and rendering are unchanged. Validation for this text-only update is a release build.
- The user reports that hints appear in new games while old saves were unreliable; intermittent disappearance still occurs in new games. This observation does not establish a save-related root cause. The release is not stable.

## 0.1.4 — 2026-10-10

- The user reported no hint during an eligible melee preview. The 0.1.3 log confirms startup but contains neither a text preparation record nor a display exception; it does not identify the exact early rejection.
- Scope damage and retaliation capture to the native `eof.bgdy` hover display call. Store the actual damage panel/range from that call rather than resolving another unit renderer. Unrelated AI/secondary-target checks cannot overwrite the hovered target's capture.
- Match the target by cell occupancy, including a large creature's non-origin cells, and keep the captured mouse cell for later validation. Scope hide invalidation to the owning hover controller.
- Do not depend on activation of the optional native simple-tooltip host. The real parent canvas, native damage preview and battle UI must still be visible.
- A newly activated TMP label with no generated glyphs now stays active and transparent for initialization instead of being repeatedly deactivated. Retain native font/material, glyph-bound layout and cursor positioning.
- Add bounded, once-per-stage diagnostics for capture, native counter refusal, visibility rejection, calculation and text preparation. No gameplay changes or relaxed retaliation eligibility.
- Build passed with zero errors/warnings. Arithmetic unchanged; native integration and visible text require the user's next gameplay test. This is not a confirmed stable release.

## 0.1.3 — 2026-10-10

- Address the icon-only cursor panel reported in the 0.1.2 gameplay test: assign both the ordinary native tooltip font and its shared material.
- Replace fixed-height ellipsis with nonwrapping overflow and explicit glyph-bound sizing/centering. Constrain the generated glyphs to the compact row rather than truncating a line because of the font's tall line metrics.
- Rebuild text only when content, scale, available width or activation changes. Keep the existing cursor position and counter eligibility logic.
- Record the first displayed text and generated glyph count in the log. Hide and diagnose a text mesh with no visible glyphs rather than drawing an unexplained empty panel.
- Build passed with no errors or warnings. Damage calculation and tests are unchanged; actual text visibility awaits the user's gameplay check.

## 0.1.2 — 2026-10-10

- Move the estimate beside the sword cursor while choosing attack direction, using the game's screen-space cursor layer instead of a world-space unit bar. Use the normal UI font without copying the bar's see-through material.
- Keep a small pixel-sized frame clear of the cursor hotspot. Follow the pointer every frame, flip at screen edges and constrain the panel to the viewport.
- Invalidate the estimate when the attack destination, counter ability or native damage endpoints change. Preserve native retaliation eligibility and casualty arithmetic.
- Hide outside the normal battle UI and when the native attack forecast disappears. No global overlay canvas, mouse interception or new gameplay hooks.
- Build, existing casualty/catalog tests and focused cursor-edge placement checks passed. Live appearance requires the user's test after restart.

## 0.1.1 — 2026-10-10

- Normalize the hint root to screen pixels on both axes; font, icon and border now use the same coordinates. This prevents the native bar's scale from turning a small background into a battlefield-sized rectangle.
- Resolve a scene camera for world-space canvases without an explicit event camera. Hide invalid/off-screen geometry and verify the actual projected size before showing it.
- Hide and invalidate the hint when the native check disallows retaliation, including ordinary shots and exhausted counters. No "Ответки нет" panel.
- Preserve native melee/reach/ranged eligibility and existing current-state casualty arithmetic. User owns visual testing after restart.

## 0.1.0 — 2026-10-10

- Initial independent retaliation estimate under the attacking unit's native bar.
- Native attack preview and retaliation eligibility; current-stat counter damage on detached health/position snapshots.
- Casualty range after defender losses, wounded-top health and preemptive retaliation.
- Native font and casualty icon, noninteractive display, no separate top-level overlay.
- Explicit unavailable state for unsupported calculations; approximate range excludes luck and triggered event chains.
- Local arithmetic/catalog validation; gameplay integration remains for user testing.
