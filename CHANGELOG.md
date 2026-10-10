# Changes

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
