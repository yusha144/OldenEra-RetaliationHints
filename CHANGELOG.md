# Changes

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
