# Retaliation Hints — isolated mod

## Distribution update 0.1.9

The user explicitly requested author yusha144 and an easy combined installer for all three mods. This authorizes coordinated packaging despite older task-specific isolation instructions below. Version 0.1.9 changes attribution/version only; no battle arithmetic or UI logic. The accepted exact 0.1.8 DLL remains in the frozen baseline, while the working plugin folder now tracks 0.1.9. Preserve stable tags, archives and baseline directories. No new Retaliation hotkey was added.

## Preserved stable 0.1.8

The user requested preserving the current 0.1.8 as stable on 2026-10-10, just like the other two mods. Frozen sibling directory `../OldenEra-RetaliationHints-STABLE-0.1.8`, archive `../OldenEra-RetaliationHints-STABLE-0.1.8.zip` and adjacent `.sha256`; local tag `stable/v0.1.8`. Never move/delete/replace this tag or the frozen baseline during ordinary development. `plugin/OldenEra.RetaliationHints.dll` is the exact accepted installed binary, not a new rebuild. Future development belongs in this working project with a higher version; preserve the installed DLL/config before updates. Recovery uses the per-file manifest and includes source, tests, actual settings, loader package/config, instructions and portable Git history. Keep all three mods independent and backups local. Stable preservation does not establish exhaustive combat or multiplayer validation. See STABLE-BASELINE.md.

## Development rules

- GUID `local.oldenera.retaliationhints`; assembly `OldenEra.RetaliationHints.dll`.
- Preserve Map Hints and Skill Synergies independently; do not alter, build, disable or reinstall either.
- Read only an actual native battle hover forecast. Do not send battle commands, modify live units, call event execution, consume the battle RNG or write saves.
- Use native deterministic damage endpoints and native retaliation eligibility. Changes to projected health/position belong only to detached copies. Never patch damage results or change the original forecast.
- Do not promise exhaustive simulation of triggered mechanics. Explicitly distinguish a current-state estimate from an exact prediction. Do not label unsupported/failed calculations as zero casualties.
- All graphics are noninteractive children of the native screen-space cursor UI and disappear with the native forecast and battle UI. The user requested placement beside the sword cursor in 0.1.2; do not reattach to the world-space unit bar or copy its see-through font material. Do not add a global overlay canvas or a sorting override.
- Normalize the mod root to screen pixels on both axes before sizing text, images or border insets. Use the screen-space canvas camera when required. Keep the cursor hotspot clear, flip at screen edges and bound the rectangle to the viewport. Hide invalid transforms; never display an oversized or inverted background.
- The user confirmed an icon-only panel in 0.1.2. For 0.1.3 use both the ordinary tooltip font and its shared material, nonwrapping overflow and measured visible glyph bounds. Do not restore fixed-height ellipsis: tall native font metrics can discard the whole first line. Preserve materials/font assets as read-only references; include the actual text and glyph count in the first-placement diagnostic. User testing must confirm visibility.
- In 0.1.3 the user again saw no panel during an eligible melee preview; the log did not identify the exact early guard. Version 0.1.4 scopes capture to the native `eof.bgdy` display call, pairs the actual panel/range with its target, checks occupied cells rather than only the unit origin, and allows a newly activated TMP label to finish preparing while transparent. Keep stage diagnostics bounded and do not claim the root cause or live rendering is confirmed without evidence.
- Since the user's 0.1.8 request, display «Без контрудара» when the native eligibility result is false, paired with the actual selected target's visible damage forecast. Do not evaluate on-hit effects in that branch. This supersedes the earlier requirement to hide on false. Unknown/unsupported calculations must never be called no counter. Do not filter on `Range`: that native enum is reaching melee, while `Shoot` is the ranged shot. Retain valid native exceptions.
- Build hash gate is mandatory because obfuscated names change across builds.
- The user performs gameplay tests. Do not start, close or control the game. Validate the arithmetic and build locally; report those checks accurately and concisely.
- Releases, history and backups stay local. Preserve installed DLL/config before updates.

- The user subsequently reported that hints appear in new games, while old saves were unreliable; intermittent disappearance still occurs in new games. Treat this as an observation, not an established save-related cause or a requirement to discard saves. Version 0.1.5 only changes the chosen label to «Потери», retaining preemptive timing and unavailable states. Do not claim the intermittent display issue is fixed.

## 0.1.6 expansion

The user requested broader mechanics after reporting missing hints for the Necropolis ghost with vampirism/temp growth, and also Temple cavalry. They speculate about restarting/new battles; no save/restart root cause is established. This request authorizes arithmetic changes beyond the earlier display-only fixes.

Version 0.1.6 pairs the actual native target view/damage and native counter decision by battle/selection/frame without a `bgdy`-only scope, and clears state on battle changes. It supports temporary creatures and bounded single-target self vampirism; known unsupported conditions produce an explicit reason. Preserve the approximate `~` label and do not promise passive event chains, luck, reflection or all healing variants. At most 257 surviving opening rolls are evaluated with healing. No native attack or heal execution is allowed. Build and focused pure tests pass; gameplay visibility/correctness are still for the user's test, not confirmed fixed.

## 0.1.7 regression fix

User reports false «эффект после попадания» on plain attacks and no panel after the first battle. Their 0.1.6 log confirms native preview pairing in multiple subsequent battles. Empty side-dealer configs are normal, including default statDmgMult=1; check an actual payload and damage target, not allocation/nonzero multiplier alone. Version 0.1.7 rebuilds the widget on every battle generation or canvas/font/material change, reacquires usable native UI, and checks actual modal visibility instead of remembered ccgc. Never reset native UI state to force a hint. Arithmetic/capture retained; build and focused regressions pass. Do not claim the user's multi-battle gameplay test passed until they confirm it.

## 0.1.8 compact counter status

The user confirmed 0.1.7 displays correctly in every new battle. They requested a concise no-counter status for attacks that suppress retaliation and asked whether vampirism is counted. Preserve native eligibility as the authority: publish false as well as true when matched to the visible damage forecast, render «Без контрудара» without invoking calculation for false. Show «Потери: нет расчёта» for unsupported/error states, retaining detailed bounded logs. Zero casualties do not mean no counter. Invalidate cached text immediately when eligibility changes. Keep the 0.1.7 battle bindings and existing limited self-vampirism arithmetic unchanged. Vampirism losses are creatures killed from the healed/grown stack, not net count change over the entire exchange. The new labels require the user's live test; other mods remain untouched.
