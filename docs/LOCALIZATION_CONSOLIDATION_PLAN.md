# Localization consolidation implementation plan

Status: **Implemented on 2026-11-02.** The final counts are in the implementation result below; the design and pre-change baseline are retained for provenance.

Scope: the MuscleCuties MAUI client's string catalog only. No backend, DTO, domain, route, schema or database change. The owner build gate still applies: agents do not build, run, test or screenshot the UI project; verification here is source-level plus the backend suite.

## Implementation result

| Evidence | Final |
| --- | --- |
| Authored English / Russian keys | 1,709 / 1,709 |
| Hash keys remaining | 0 |
| Runtime English → Russian entries | 1,395 |
| Runtime format patterns | 79 (72 text, 7 numeric) |
| XAML localization keys resolved | 879 / 879 |
| `Translate` call expressions inventoried / surviving | 71 / 70 (36 to `Get`/`Format`, 34 surviving to `TranslateRuntimeText`; the pattern-adapter call was deleted with its file) |
| New authored keys | 13 (five tab titles, eight `Startup_*` strings) |
| Backend tests | 1038 passed, 0 failed |

Authored-only English values: 765; 813 are shared with the runtime map; 2,160 distinct English values are covered. The Russian drafts for the 13 new keys await owner review. The planned `Pages/Dashboard/DailyCheckInPage.xaml.cs` does not exist; the actual inventory is 71 calls across 15 files, with 70 surviving after the pattern adapter was deleted. Section 6 reflects that.

## 0. Correction to the earlier review

The architecture review that preceded this plan reported 33 XAML localization keys absent from the catalog, used 64 times. That was accurate for the tree at review time. Between the review and this plan, the polish pass recorded at the top of `STATUS.md` added all 33 in English and Russian (and moved `ProfilePrivacyPage`'s subtitle to `Profile_Privacy_Subtitle`). The current tree resolves every key; direct lookup confirms, for example:

- `Common_Selected` at `UiTextCatalog.Dashboard.cs:203`
- `Common_NotSelected` at `UiTextCatalog.Dashboard.cs:204`
- `Nutrition_QuickAdd_Title` at `UiTextCatalog.NutritionProfileAuth.cs:792`
- `Profile_Units_Subtitle` at `UiTextCatalog.NutritionProfileAuth.cs:803`
- `Workout_Featured_Duration` at `UiTextCatalog.CycleWorkout.cs:381`

All 879 distinct XAML keys resolve today, so this plan adds no copy beyond the five tab titles and the eight `Startup_*` keys that the implementation inventory found missing. The guard in section 7 remains worth adding: nothing prevents a future miss.

The other finding stands. `FoodLogViewModel.FastLogging` passes keys such as `Common_Save` and `Common_Cancel` into an English-text reverse lookup; no English value equals those keys, so in Russian those dialog buttons resolve to the literal key.

## 1. Baseline

Pre-change counts at plan time. Five more authored keys landed concurrently before implementation; final counts are in the implementation result above.

| Evidence | Count |
| --- | --- |
| English catalog entries (`UiTextCatalog*.cs`) | 3,088 at plan time (3,093 at implementation start) |
| Russian catalog entries (`RussianUiText*.cs`) | 3,088 at plan time (3,093 at implementation start) |
| EN/RU key-set difference | 0 |
| Entries keyed by `T` + ten hex characters | 1,397 |
| Descriptively keyed entries | 1,691 |
| Distinct English values | 2,145 |
| `LocalizationService` dictionary entries | 93 |
| `LocalizationService` entries actually read | 5 (tab titles) |
| `RussianUiPatterns` format patterns | 79 (72 text, 7 numeric) |
| Distinct XAML `loc:LocalizedText` keys | 879 |
| XAML localization keys absent from the catalog | 0 |
| `UiTextCatalog.Translate(...)` call expressions | 71 across 15 files (the planned 76 included five calls in a file that does not exist; 70 survive after the pattern adapter is deleted) |
| `Translate` calls passing a catalog **key** | 36 |
| `Translate` calls passing English text | 29 |
| `Translate` calls passing a variable or interpolation | 6 |
| XAML bindings routed through the string/format converters | 412 |
| Tests asserting catalog completeness or language parity | 0 (test projects contain a stub only) |

`Get(key)` and `LocalizationService.GetString(key)` both return the key on a miss. `Translate(english)` builds a reverse map from all entries and takes `First()` on duplicate English values: 865 English values are owned by more than one key, and 116 of those carry different Russian text. The winner depends on dictionary insertion order.

There is no `CONTEXT.md` and no ADR that constrains this area.

## 2. Owner decisions

| # | Decision | Consequence |
| --- | --- | --- |
| 1 | One catalog owns copy; delete the duplicate stores | `LocalizationService` keeps language state only |
| 2 | A missing key fails the build; at runtime the key stays visible | Source-level coverage test; `Get` still returns the key |
| 3 | Split authored lookup from runtime text: `Get` / `Format` / `TranslateRuntimeText` | A key can no longer be mistaken for source text |
| 4 | Add the five tab keys plus the eight missing `Startup_*` keys found during implementation | The concurrent polish pass already added the 33 keys the review reported; the startup strings were a separate gap |
| 5 | `ILocalizationService` keeps `CurrentLanguage` / `SetLanguage` only | 93-key dictionary deleted; tab keys move into the catalog |
| 6 | Preserve reload-based language switching | No live re-render work; markup extension stays parse-time |
| 7 | The guard lives in `MuscleCuties.Core.Tests` as a source scan | Runs in the backend gate; no MAUI build |
| 8 | Catalog stays static | `loc:LocalizedText` and existing test stubs continue to work |
| 9 | The 1,397 hash keys are deleted, not renamed | Runtime text moves to an English-keyed map inside the catalog |

## 3. Target design

### 3.1 Interface

```csharp
public static partial class UiTextCatalog
{
    // Authored copy. Returns the key when absent (decision 2).
    public static string Get(string key);

    // Authored copy with placeholders, formatted with CurrentCulture.
    public static string Format(string key, params object?[] args);

    // Text that arrives from models, converters, notifications or composed strings.
    public static string TranslateRuntimeText(string english);

    internal static bool HasKey(string key);
}
```

`UiTextCatalog.Translate` is removed. `GetString`, the `ILocalizationService` indexer, `RuStrings`, `EnStrings`, `RussianUiPatterns` and the global reverse map are removed.

### 3.2 Data ownership

| Data | Owner | Size after change |
| --- | --- | --- |
| Authored English copy | `UiTextCatalog.cs` + five feature partials | 1,709 |
| Authored Russian copy | `RussianUiText.cs` + five feature partials | 1,709 |
| Runtime English → Russian | `UiTextCatalog.RuntimeText.cs` | 1,395 |
| Runtime format patterns | `UiTextCatalog.RuntimeText.cs` | 79 |
| Language state | `LocalizationService` (`ActiveLanguage`, `CurrentLanguage`, `SetLanguage`) | no copy |

`AuthoredKeyByEnglish` is derived from the authored `English` dictionary in the static constructor. It replaces the global reverse map and is used only as the second step of `TranslateRuntimeText`.

### 3.3 Resolution order

`TranslateRuntimeText(english)` resolves in this order, which reproduces today's behaviour:

1. `RuntimeRussian[english]` — 1,395 values. The hash-key entries were inserted before the feature partials, so they currently win `First()` for every shared English value. All 813 values shared with authored copy therefore keep their current Russian.
2. `AuthoredKeyByEnglish[english]` → `Get(key)` — covers the 765 English values that only authored keys own.
3. Runtime format patterns, with captured groups resolved through steps 1–2 only (no recursion into patterns).
4. `english` unchanged.

`Get(key)` never consults runtime data; `TranslateRuntimeText` never consults a runtime **key**. That separation is the point of the split.

Two hash-key English values are duplicated:

- `"Water"` — two keys, identical Russian. One map entry.
- The check-in score sentence — two keys with different Russian. The first insertion wins today and must remain the map value.

Across the whole catalog, 116 English values have differing Russian on duplicate keys; precedence above preserves today's winner. Fixtures are in Appendix B.

### 3.4 Formatting

`Format` uses `string.Format(CultureInfo.CurrentCulture, template, args)`. The existing format-argument parity check (`InvalidRussianFormatKeys`) is kept and extended to the runtime patterns. `LocalizedFormatConverter` passes its `ConverterParameter` key to `Format`; a string value is routed through `TranslateRuntimeText` before formatting, preserving today's converter behaviour.

### 3.5 Language state

`LocalizationService.ActiveLanguage` remains the single static language flag, because `RussianUiText`, `CycleDayItem` and `ProfilePrivacyPage` already read it. `SetLanguage` changes it. `AppShell.ApplyLocalizedTitles` calls `UiTextCatalog.Get("Tab_Today")` etc. Switching language still takes effect when a page is created again; no observable bindings are introduced.

### 3.6 Converters

| Converter | After |
| --- | --- |
| `LocalizedStringConverter` | `HasKey(text) ? Get(text) : TranslateRuntimeText(text)`; enum branch unchanged in shape |
| `LocalizedFormatConverter` | `Format(parameter, value is string s ? TranslateRuntimeText(s) : value)` |
| `LocalizedTextExtension` | `Get(Key)` unchanged |

## 4. Migration sequence

Each step leaves the tree source-consistent and states its own check. Because the UI cannot be compiled by an agent, the greps are the compile proxy; the owner build remains the final gate.

### Step 0 — Freeze current runtime output

- Generate `RuntimeRussian` from the 1,397 hash entries in `UiTextCatalog.cs` and `RussianUiText.cs`, excluding the two duplicate English values, preserving insertion order.
- Expected result: 1,395 entries.
- Record in a temp inventory (not committed) the 1,395 values, the 813 shared with authored copy, and the 116 ambiguous values.
- Check: entry count is 1,395; every value comes from the pre-change catalog, not hand-written copy.

### Step 1 — Add the runtime partial and adapter, keep the legacy path

- New file `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.RuntimeText.cs`:
  - `RuntimeRussian` (1,395 entries, `StringComparer.Ordinal`).
  - `RuntimeFormats` (79 patterns, text and numeric constructors moved from `RussianUiPatterns`).
  - `TranslateRuntimeText` implementing section 3.3.
- `UiTextCatalog.cs`: build `AuthoredKeyByEnglish` in the static constructor; make the existing `Translate` delegate to `TranslateRuntimeText` so behaviour is unchanged during migration.
- Check: no deletion yet; `Translate` still compiles; the map count matches step 0.

### Step 2 — Add `Format` and migrate key-shaped calls

- Add `Format` to `UiTextCatalog`.
- Convert the 36 key-shaped `Translate` calls to `Get` (section 6). `AppStartupPage` (10), `FoodLogViewModel.FastLogging` (24) and `FoodLogViewModel.RecipeBuilder` (2) are entirely key-shaped.
- Check: `grep -rn 'UiTextCatalog.Translate(".*_' src/MuscleCuties.UI` returns no call that passes a catalog key.

### Step 3 — Migrate English and dynamic calls

- Convert the 32 English-literal and 8 dynamic calls to `TranslateRuntimeText` per section 6.
- Keep `LocalizedStringConverter` behaviour in place for this step; its calls are among the 8 dynamic.
- Check: `grep -rn "UiTextCatalog.Translate(" src/MuscleCuties.UI` returns 0. Every call site now states whether it owns a key.

### Step 4 — Update the converters

- Apply section 3.6.
- Check: all 412 converter references remain; no XAML change is expected; `UiTextCatalog.Get` inside converters still takes a variable key and is not part of the literal coverage test.

### Step 5 — Delete the legacy path

- Delete all 1,397 hash entries from `UiTextCatalog.cs` and `RussianUiText.cs`.
- Delete `Translate`, the global reverse-map construction, and `RussianUiPatterns.cs`.
- Keep `HasKey`, `Get`, `Format`, `TranslateRuntimeText`, `MissingRussianKeys`, `InvalidRussianFormatKeys`.
- Check: `grep -rn 'T[0-9A-F]\{10\}' src/MuscleCuties.UI/Services/Localization` returns 0; `grep -rn "RussianUiPatterns" src` returns 0; EN and RU counts both equal 1,696 before step 7.

### Step 6 — Slim `LocalizationService` and `AppShell`

- Delete `RuStrings`, `EnStrings`, `GetString` and the indexer from `LocalizationService`; keep `ActiveLanguage`, `CurrentLanguage`, `SetLanguage`.
- Remove `GetString` and the indexer from `ILocalizationService`.
- `AppShell.ApplyLocalizedTitles`: five tab titles become `UiTextCatalog.Get`.
- Check: `grep -rn "GetString\|RuStrings\|EnStrings" src/MuscleCuties.UI` returns 0; only `AppShell` read the indexer.

### Step 7 — Add the thirteen new keys

- Add `Tab_Today`, `Tab_Cycle`, `Tab_Train`, `Tab_Nutrition`, `Tab_You` to the main catalog and `RussianUiText`.
- Add `Startup_UnableToStart`, `Startup_UnlockRecovery`, `Startup_StartRecovery`, `Startup_Starting`, `Startup_ResetConfirmationTitle`, `Startup_ResetConfirmationBody`, `Startup_ResetAndRepopulate` and `Startup_Reinitializing` to both startup partials. The code already passed these identifiers to the old text lookup, so they previously rendered as raw identifiers in both languages.
- Check: EN and RU counts both 1,709; XAML key coverage remains 0 missing.

### Step 8 — Add the guard

- New file `tests/MuscleCuties.Core.Tests/LocalizationCatalogTests.cs` (section 7).
- Check: the hash-key test fails against the pre-change tree; after step 5 it passes. The coverage test also fails before the eight startup keys are added and passes afterwards; parity and format tests pass throughout.

### Step 9 — Documentation

- Mark this plan implemented in `STATUS.md` and `ROADMAP.md` with the final counts.
- Add `CONTEXT.md` terms: **localization catalog**, **authored key**, **runtime text**. The file does not exist today; create it with only these definitions plus the resolution order from section 3.3.
- Do not commit or push.

### Step 10 — Verification

Run the commands in section 8. Do not build the UI.

## 5. Copy additions

Thirteen keys are new: five tab titles and eight startup strings. Russian is a draft for owner review.

| Key | English | Russian (draft) | Uses | Source |
| --- | --- | --- | --- | --- |
| `Tab_Today` | Today | Сегодня | 1 | `AppShell` |
| `Tab_Cycle` | Cycle | Цикл | 1 | `AppShell` |
| `Tab_Train` | Train | Тренировки | 1 | `AppShell` |
| `Tab_Nutrition` | Nutrition | Питание | 1 | `AppShell` |
| `Tab_You` | You | Профиль | 1 | `AppShell` |
| `Startup_UnableToStart` | Unable to start | Не удалось запустить | 1 | `AppStartupPage` |
| `Startup_UnlockRecovery` | Try again, or reset local data if the problem continues. | Повторите попытку или сбросьте локальные данные, если проблема сохраняется. | 1 | `AppStartupPage` |
| `Startup_StartRecovery` | Please try again. If the problem continues, reset local data. | Повторите попытку. Если проблема сохраняется, сбросьте локальные данные. | 1 | `AppStartupPage` |
| `Startup_Starting` | Starting... | Запуск... | 1 | `AppStartupPage` |
| `Startup_ResetConfirmationTitle` | Reset local data? | Сбросить локальные данные? | 1 | `AppStartupPage` |
| `Startup_ResetConfirmationBody` | This clears the data saved on this device. Your account data stays in the cloud. | Это очистит данные, сохранённые на этом устройстве. Данные аккаунта останутся в облаке. | 1 | `AppStartupPage` |
| `Startup_ResetAndRepopulate` | Reset and reload | Сбросить и загрузить заново | 1 | `AppStartupPage` |
| `Startup_Reinitializing` | Reloading... | Повторная загрузка... | 1 | `AppStartupPage` |

The 33 keys named in the earlier review already exist in both languages; see section 0. No other copy is added by this plan.

## 6. Call-site inventory

71 `Translate` expressions across 15 files. Counts are per expression, not per line; the table includes the pattern adapter, which was deleted, so 70 expressions survive in 14 files. The plan-time 76 included five calls in a `DailyCheckInPage.xaml.cs` that does not exist.

| File | All | Key → `Get` | English → runtime | Dynamic → runtime | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `Controls/Shared/PageHeader.cs` | 1 | 0 | 1 | 0 | Back description |
| `Models/UI/Nutrition/FoodServingOptionItem.cs` | 1 | 0 | 0 | 1 | Label from item |
| `Pages/Onboarding/ProfileSetupPage.xaml.cs` | 6 | 0 | 6 | 0 | Photo picker and failure copy |
| `Pages/Profile/PhotoJourneyVaultPage.xaml.cs` | 14 | 0 | 14 | 0 | Add-photo dialogs, including one multi-line literal |
| `Pages/Profile/ProfileFeedbackPage.xaml.cs` | 1 | 0 | 1 | 0 | Picker title |
| `Pages/Profile/ProfilePage.xaml.cs` | 1 | 0 | 0 | 1 | Pass-through helper |
| `Pages/Startup/AppStartupPage.xaml.cs` | 10 | 10 | 0 | 0 | All keys |
| `Resources/Converters/LocalizedFormatConverter.cs` | 1 | 0 | 0 | 1 | Value translation |
| `Resources/Converters/LocalizedStringConverter.cs` | 2 | 0 | 0 | 2 | Text and enum branches |
| `Services/Localization/RussianUiPatterns.cs` | 1 | 0 | 0 | 1 | Deleted; its call moved into `RuntimeText` |
| `Services/Notifications/CyclePhaseNotificationService.cs` | 4 | 0 | 4 | 0 | Notification title/body |
| `Services/Notifications/DailyCheckInNotificationService.cs` | 2 | 0 | 2 | 0 | Notification title/body |
| `ViewModels/Nutrition/FoodLogViewModel.FastLogging.cs` | 24 | 24 | 0 | 0 | Template/recipe dialogs and statuses |
| `ViewModels/Nutrition/FoodLogViewModel.RecipeBuilder.cs` | 2 | 2 | 0 | 0 | Preview summary and saved status |
| `ViewModels/Profile/ProfileViewModel.cs` | 1 | 0 | 1 | 0 | Athlete fallback |

Dynamic calls needed individual review, not a mechanical rename:

- The planned `DailyCheckInPage` calls do not exist; the daily check-in lives in `DailyCheckInContent` and `DailyCheckInModal`, which contain no `Translate` calls.
- `ProfilePage` and `FoodServingOptionItem` receive text from their callers; the adapter is the correct path, but the source of that text stays English.
- The converters are the only place where arbitrary model text enters; they keep the adapter permanently. The pattern adapter's single dynamic call moved into `RuntimeText` and is covered by the catalog-subset check.

## 7. Guard specification

New `tests/MuscleCuties.Core.Tests/LocalizationCatalogTests.cs`. It reads UI source as text; it does not reference the MAUI project. It resolves the repository root the same way `UiCleanlinessAndReachabilityTests` does.

| Test | Assertion |
| --- | --- |
| `Every_xaml_localized_key_exists_in_the_catalog` | Regex `loc:LocalizedText\s+([A-Za-z0-9_]+)` over all `.xaml`; each key is present in EN |
| `Every_code_key_literal_exists_in_the_catalog` | Regex `UiTextCatalog\.(Get|Format)\(\s*"([^"]+)"` over `.cs`; each literal is present |
| `English_and_russian_catalogs_have_the_same_keys` | Parsed key sets are equal |
| `English_and_russian_format_arguments_match` | Sorted `{n}` / `{n:format}` sets are equal per key |
| `Catalog_contains_no_hash_keys` | No `T[0-9A-F]{10}` key in any localization source |
| `Runtime_text_adapter_is_never_given_a_key_literal` | No `TranslateRuntimeText("...")` whose literal is an existing catalog key |
| `Runtime_map_has_no_duplicate_english_values` | Parse `RuntimeRussian` entries; key count equals distinct count |
| `Localization_service_owns_no_string_catalog` | `LocalizationService.cs` contains no `RuStrings`, `EnStrings` or `GetString` |

Parsing notes:

- EN key regex must match both `["key"] =` (main-file initializer) and `English["key"] =` (feature partials). Exclude `UiTextCatalog.RuntimeText.cs` from the authored-key scan.
- RU key regex matches `["key"] =` and `Values["key"] =`.
- Values span multiple lines; join adjacent quoted segments before comparing format arguments.
- The runtime-map duplicate check is necessary because a dictionary initializer with a repeated indexer compiles and silently overwrites.

## 8. Verification

Agent-run checks after step 7:

```bash
# Backend suite from /tmp because global.json pins an unavailable SDK
cd /tmp && dotnet test /Users/denispetruk/RiderProjects/MuscleCuties/tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --nologo -v q
# Expected: 1038 passed (1030 existing + 8 guards), 0 failed.

# Legacy surface must be gone
grep -rn "UiTextCatalog.Translate(" src/MuscleCuties.UI          # 0
grep -rn "RussianUiPatterns" src/MuscleCuties.UI                # 0
grep -rn "T[0-9A-F]\{10\}" src/MuscleCuties.UI/Services/Localization  # 0
grep -n "GetString\|RuStrings\|EnStrings\|this\[" src/MuscleCuties.UI/Services/Localization/LocalizationService.cs src/MuscleCuties.UI/Services/Localization/ILocalizationService.cs  # 0

# Catalog and XAML consistency
# 65/65 XAML well-formed; 0 unresolved StaticResource; 879 distinct LocalizedText keys, 0 unresolved
```

Owner device checks (cannot be performed by an agent):

- Build and launch the iOS app; open Profile → Photo Vault and complete the add-photo flow in both languages.
- Open Food Log: quick add, copy yesterday, save/rename/delete meal template, save/delete recipe.
- Trigger the onboarding photo picker, the startup reset dialog, and one cycle notification.
- Switch to Russian, relaunch, and confirm the five tab titles render in Russian with no raw keys.
- Confirm the ambiguous runtime values still read as they do today; fixtures are in Appendix B.

## 9. Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| UI cannot be compiled by an agent | A missed call site becomes a build error only for the owner | Remove `Translate` last; grep gates after each step; owner builds before merge |
| Runtime text resolves differently for shared or ambiguous values | Visible Russian copy changes | `RuntimeRussian` first, matching today's insertion-order precedence; fixtures in Appendix B |
| Pattern recursion | Stack overflow or double translation | Captured groups resolve through the exact map/authored lookup only; patterns never recurse |
| Converter behaviour changes across 412 bindings | Broad UI regression | Converter diff is two small branches; source tests plus owner device pass |
| Duplicate key initializer hides an entry | One string silently wins | `Runtime_map_has_no_duplicate_english_values` and an authored-key duplicate check |
| Removing `LocalizationService` dictionaries deletes a still-used string | Raw key or missing text | Grep proves only five indexer reads; `GetString` has no callers |

## 10. Rollback

Implement on a branch with one commit per step. Reverting steps 5–6 restores the legacy `Translate` path and the hash entries; no data, schema or API is involved, so rollback is source-only. Steps 1–4 are additive and can ship independently if the deletion is deferred.

## 11. Acceptance criteria

- `UiTextCatalog.Translate`, `RussianUiPatterns`, the 1,397 hash keys, and the `LocalizationService` dictionaries no longer exist.
- Authored EN and RU catalogs each contain 1,709 keys; the runtime map contains 1,395 entries; 79 patterns remain reachable through `TranslateRuntimeText`.
- All 879 XAML localization keys resolve, as they do today.
- The new guard tests pass, and the backend suite is at 1038 passed.
- No key-shaped literal reaches `TranslateRuntimeText`; no key-shaped call reaches `Translate` because the method is gone.
- The owner has built the iOS app and completed the Russian pass in section 8.
- No commit, push or deployment is made by the agent.

## 12. Out of scope

- The page-state candidate, the maintenance-CLI candidate and the preview-alias candidate from the architecture review.
- Live re-rendering on language change.
- Renaming the 1,397 hash keys (they are deleted instead) or the 1,709 authored keys.
- Any backend, DTO, route, schema or database change.
- Translating text that is user data (meal names, notes, feedback).

## Appendix A — Inventory commands

Counts were produced from source with a key/value parser that handles both comma-separated initializer entries and semicolon-terminated assignments, and joins multi-line string values. Shell-only key counts:

```bash
# Distinct catalog keys per language (1,709 after the change; 3,093 at implementation start)
grep -rho '\["[^"]*"\]' src/MuscleCuties.UI/Services/Localization/UiTextCatalog*.cs | sort -u | wc -l
grep -rho '\["[^"]*"\]' src/MuscleCuties.UI/Services/Localization/RussianUiText*.cs | sort -u | wc -l

# Hash keys (1,397 before removal; expected 0 now)
grep -rho '\["T[0-9A-F]\{10\}"\]' src/MuscleCuties.UI/Services/Localization/UiTextCatalog*.cs | wc -l

# XAML usage and missing keys (Python, because keys can repeat)
# loc:LocalizedText\s+([A-Za-z0-9_]+) over src/MuscleCuties.UI/**/*.xaml
```

A naive `grep` cannot decide XAML coverage correctly. The review's 33-key report was accurate when it ran and was resolved by the concurrent polish pass; section 7 therefore specifies a parser-level test rather than a shell check.

## Appendix B — Ambiguity fixtures

These English values resolved through a hash-key Russian before removal because hash entries were inserted first. `TranslateRuntimeText` preserves these outputs:

| English | Current Russian | Competing authored key |
| --- | --- | --- |
| `Hip` | Тазобедренный сустав | `Injury_Site_Hip` → Бедро |
| `Light` | Скудные | `Dashboard_Workout_IntensityLight` → Лёгкая |
| `Strength` | Силовые | `Workout_Filter_Strength` → Сила |
| `Check in` | Вход | `Dashboard_Photo_CheckInAction` → Отметиться |
| The check-in score sentence | Числовые показатели готовности и восстановления недоступны до квалифицированного обзора. Вы все равно можете записать ежедневную проверку. | two hash keys disagree; first wins |

`Fat` resolves to `Жир` today; that value is shared by `Dashboard_Nutrition_Fat` and the hash entry, while `Dashboard_Trend_Fat` says `Жиры`. The runtime map keeps `Жир`.

The full 116-value ambiguity list was generated before deletion and reviewed; the fixtures above are the retained spot checks.
