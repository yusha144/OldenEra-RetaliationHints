# Изменения Retaliation Hints

## 0.1.13, предварительный выпуск

- Добавлены отдельные установка и удаление с резервными копиями, сохранением настроек и других модов.
- При запуске из исходников появляется объяснение со ссылкой на выпуски.
- README и выпуск явно обозначают неполноту расчёта и приблизительность результата. Добавлены инструкции, MIT и формы обратной связи.
- Формула прогноза и наблюдение боевых событий сохранены из 0.1.12. Номер версии и организация сборки обновлены.

## Тестовая поставка 0.1.12 - 2026-10-18

- Подготовлен отдельный архив для первого тестирования в Discord. Версия расчёта и поведение подсказки не изменены.
- В README и инструкции явно описаны ограничения: удача и неудача, изменения характеристик и лечение после перемещения, повторные атаки, отражение и сложные цепочки эффектов. Подробная сверка по умолчанию выключена; существующие настройки при обновлении сохраняются.
- Новый игровой сеанс: 89 команд в 9 боях, 36 оцениваемых действий. Потери совпали во всех 36, но обычная ответка состоялась только в двух. Есть 5 расхождений первого урона при изменении состояния до удара. Полная точность не заявляется.
- Подтверждено сохранение шагов двух последовательностей с повторным ударом. Их подробный урон не записан из-за отсутствия связанного прогноза; это ограничение диагностики сохраняется.

## 0.1.12 - 2026-10-17

- Исправлена сверка добивающего урона. Событие игры сохраняет полный урон; оставшимся здоровьем ограничивается только изменение здоровья. Ошибка 0.1.11 давала 11 ложных расхождений в записанном сеансе.
- Служебные симуляции отсеиваются по экземпляру открытого боя и его очереди до создания записей и изменения ожидающего прогноза.
- Все шаги одного вызова игрового планировщика получают общую запись. Отдельные команды хранятся независимо, следующая атака не стирает предыдущую. Многоударные последовательности сохраняются, но не оцениваются как одиночная ответка.
- Формат журнала 2 сохраняет границы команд и идентификаторы шагов. Запись продолжается в следующем файле после 16 МиБ без удаления прежних частей. Удача и неудача по-прежнему исключаются, включая последующие удары.
- Сборка без ошибок и предупреждений. Пройдены 97 проверок наблюдателя, прежние проверки и чтение 40 шаблонов атак. Повторная обработка 971 сохранённого итога: 42 совпадения, 11 исправленных ложных расхождений, 849 записей чужих экземпляров боя. В старом формате нет идентификаторов очереди; этот дополнительный фильтр проверен отдельно. Потерянные события записи 211 не восстановлены и не выдумывались.
- Формула прогноза не изменена. На момент этой сборки новая привязка к планировщику была проверена по игровым файлам и отдельным тестам. Результаты последующего игрового сеанса описаны в разделе тестовой поставки от 18 октября.

## 0.1.11 - 2026-10-17

- Добавлена необязательная автоматическая сверка через Diagnostics.CombatAudit. Сохраняются показанный прогноз, игровые события урона и потерь, состояния отрядов и итог после завершения действия.
- Удача и неудача исключаются из оценки по уже определённым игрой множителям. Наблюдатель не вызывает RNG, команды боя или обработчики событий.
- Неоднозначная связь, пропущенные события, несколько ударов, упреждающие ответки и незавершённые действия имеют отдельные статусы. Они не подменяются нулём или успешной проверкой.
- Проверяются диапазон потерь, штатный диапазон первого урона и переход здоровья. Формула подсказки не изменена. Выбранная клетка сохраняется для сверки также при отсутствии ответки.
- Добавлены подробный журнал JSONL, краткая сводка, ограничение размера журнала и инструкция с минимальным набором игровых ситуаций.
- Сборка без предупреждений и ошибок; 56 новых автоматических утверждений для сверки, прежние проверки и чтение 40 шаблонов атак из установленной игры прошли. Подключение к настоящему бою ещё требует первого игрового сеанса.

## 0.1.10 - восстановленная версия

- По ранее принятому решению авторство оставлено строкой в README. Из метаданных DLL и сообщения загрузки авторство удалено. Рабочая версия восстановлена из старых материалов; расчёт боя не изменён.

## 0.1.9 — 2026-10-10

- Credit yusha144 in assembly metadata, README and startup log.
- Included in the shared BAT installer distribution. No changes to casualty arithmetic, supported effects, labels, capture or battle lifecycle.
- Stable 0.1.8 remains frozen. Live game testing belongs to the user.

## 0.1.8 — 2026-10-10

- User confirmed 0.1.7 displays correctly in every new battle.
- Publish both allowed and disallowed native counter decisions, still paired with the actual selected target's visible damage forecast. Show «Без контрудара» on native refusal without evaluating on-hit mechanics or damage.
- Keep numeric ordinary/preemptive estimates for allowed counters. Zero casualties and unavailable calculations are never mislabeled as no counter. Counter eligibility changes immediately invalidate the cached display.
- Replace detailed unavailable text with «Потери: нет расчёта»; retain bounded diagnostic reasons in the log.
- Health/vampirism arithmetic and per-battle widget lifecycle are unchanged. Self vampirism already includes healing and temporary growth before an ordinary counter, within the documented single-source/single-target limits.
- Release build passed with zero warnings/errors. Existing checks and one focused status regression group passed: allowed/refused transitions, bypassed effect calculation, zero casualties, preemptive text and unavailable estimates. New live labels remain for the user's test.

## 0.1.7 — 2026-10-10

- Fix false on-hit-effect rejection for normalized empty side-dealer configs. Inspect actual buffs, mechanics and damage to a configured target; allocation, pattern metadata and the default statDmgMult=1 alone do not block ordinary attacks.
- Recreate the hint for each battle generation and for changed canvas/font/material, even when the native cursor host is pooled. Discard stale UI lookup and widget references.
- Resolve an active native battle UI, with a throttled read-only scene lookup if its singleton is unusable. Inspect visible unit/book/result windows and the open battle log instead of treating remembered ccgc mode as current modal visibility. Never write native UI mode or activation.
- Reset bounded stage diagnostics per battle and record prepared text per binding. The user's log shows successful preview pairing in battles 2, 4, 6, 9 and 11 despite missing display; capture and casualty arithmetic are otherwise retained.
- Release build passed with zero errors/warnings. Existing checks plus regressions for empty normalized dealers and successive pooled battle bindings pass. Actual multi-battle visibility remains for the user's gameplay test.

## 0.1.6 — 2026-10-10

- Join the actual native target panel and counter eligibility by battle, hover, caster, target, ability, cell and frame. Remove dependency on a single `bgdy` display entry; reset on battle identity changes. Retain native counter refusal and cursor visuals.
- Support existing temporary creatures and single-target self vampirism: heal modifiers, immunity checks, native healing caps, temporary growth, capped real resurrection, and preemptive ordering. Count casualties from the healed stack rather than net change in army size.
- Sweep intermediate opening damage for healing (at most 257 surviving rolls); explicitly reject wider ranges instead of sampling an unreliable bound or stalling the game. Lethal rolls need no counter calculation.
- Show concise reasons for recognized unsupported mechanics, death protection, direct on-hit buffs and special healing. Other passive event chains, luck, reflection and movement-triggered effects remain outside the current-state estimate.
- Add bounded per-battle/matchup pairing diagnostics. The user also reported missing previews for Temple cavalry and before the ghost's first hit; this is distinct from temporary-stack arithmetic. Save/restart causality is unconfirmed.
- Release build: zero errors/warnings. Focused pure health, healing, selection identity, cursor and installed-pattern checks pass. Native integration and intermittent visibility still require the user's gameplay test; not a stable release.

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
