# Retaliation Hints — isolated mod

- GUID `local.oldenera.retaliationhints`; assembly `OldenEra.RetaliationHints.dll`.
- Preserve Map Hints and Skill Synergies independently; do not alter, build, disable or reinstall either.
- Read only an actual native battle hover forecast. Do not send battle commands, modify live units, call event execution, consume the battle RNG or write saves.
- Use native deterministic damage endpoints and native retaliation eligibility. Changes to projected health/position belong only to detached copies. Never patch damage results or change the original forecast.
- Do not promise exhaustive simulation of triggered mechanics. Explicitly distinguish a current-state estimate from an exact prediction. Do not label unsupported/failed calculations as zero casualties.
- All graphics are noninteractive children of the native unit bar and disappear with the native forecast and battle UI.
- Normalize the mod root to screen pixels on both axes before sizing any text, images or border insets. Native unit-bar/world-space units are not pixels. Use an actual camera for world-space projection, and verify the projected rectangle before revealing it. Never display an oversized or inverted background on a failed transform.
- Hide the entire hint when the native eligibility result is false, including shots without counterattacks and exhausted/prohibited counters. Clear an old capture when eligibility becomes false. Do not filter on `Range`: that native enum is reaching melee, while `Shoot` is the ranged shot. Retain valid native exceptions.
- Build hash gate is mandatory because obfuscated names change across builds.
- The user performs gameplay tests. Do not start, close or control the game. Validate the arithmetic and build locally; report those checks accurately and concisely.
- Releases, history and backups stay local. Preserve installed DLL/config before updates.
