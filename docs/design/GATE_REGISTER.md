# Current UI gate register — 2026-11-03

This is the current gate checklist for the UI/style/navigation mission. It supersedes historical “open” wording in `STATUS.md` and `ROADMAP.md`; those entries remain provenance, not current certification. Local automated gates are green. **2026-11-04:** the owner explicitly authorized an agent UI build/run pass; UI build **0 warnings / 0 errors**, Core **1055/1055**, Mobile Contracts **16/16**, Server build **0/0**. Deployed staging latency is measured and **failing**; production is undeployed, so production p99 is unavailable. The remaining acceptance work is **owner device verification** and **deployed performance/release**.

| Gate or rule | Proof run on the current tree | Result |
| --- | --- | --- |
| Pinned backend toolchain | `global.json` requests SDK `10.0.200` with `latestFeature`; local run resolved installed `10.0.203`, while CI can still take `10.0.403` | Pass |
| Core rules and source guards | `dotnet test tests/MuscleCuties.Core.Tests` | **1055/1055** (includes `Path_data_bindings_convert_string_paths_to_geometry`) |
| Mobile contract tests | `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests` | **16/16** |
| Registration tests | `dotnet test tests/MuscleCuties.Registration.Tests` | **39/39** |
| Server tests | `dotnet test tests/MuscleCuties.Server.Tests` with `MC_LATENCY_POSTGRES` | **113/113** |
| All-route server latency | `bash scripts/measure-server-latency.sh`; `ServerRouteParityTests.EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget`; checked-in report at `docs/performance/server-latency-report.json` | **Pass** — 155/155 routes, 0 over budget; max cold **817.61 ms**, max warm p95 **44.06 ms**; startup/readiness min/median/max **434.94 / 524.78 / 1062.42 ms**; fresh documented-command run exited 0 with status codes on all 155 entries |
| Deployed Lambda latency | `bash scripts/measure-deployed-latency.sh`; checked-in report at `docs/performance/deployed-latency-report.json` | **Open** — the 2026-11-03 window recorded p99 **6,895.20 ms** / warm p99 **5,392.18 ms** at 1024 MB with no route attribution. The 2026-11-04 deploy raised memory to **3008 MB** and added per-route telemetry; warm handler time now measures 2–5 ms (`/health/live`) and 29–40 ms (`/health/ready`) with init **1,046.74 ms**. Re-measurement over a new window is required before this can be called green, and no production deployment exists |
| Per-route latency attribution | `RequestTimingTelemetry` (`Request {Method} {RouteTemplate} {StatusCode} {ElapsedMilliseconds}`); API Gateway detailed metrics enabled on `$default` | **Pass (instrumentation)** — verified live in CloudWatch; records no query string, body, header, token or identifier. Attribution of the historical tail is still pending a traffic window |
| Deployed API reachable after release | Public probes on version 11 | `/health/live` **200**, `/health/ready` **200 `Healthy`**, registration quiz **200**, unauthenticated `/api/v1/session` **401** |
| Deployed package artifact | `dotnet publish -c Release -r linux-arm64 --self-contained false`; 35 files, 8,656,565 bytes, ARM64 apphost, no PDBs | **Pass** — SHA-256 `11aa894ff220777c54f4fdcf70a146f61860aab43ec6b5d05999d52362197eda`; all required assemblies, `appsettings.json` and ARM64 `libSkiaSharp.so` present |
| Database-backed integration | disposable PostgreSQL 17; seeded `mc_test_nutrition_ci`, `mc_retry_disposable`, restricted `mc_pg09_disposable`; all five `MC_*_POSTGRES` fixtures | **96/96** |
| All UI XAML parses | `xmllint --noout` over every non-generated `.xaml` | Pass |
| Owner walkthrough is runnable | `bash -n scripts/verify-ui-flows.sh`; 16 stages match `TOTAL_STAGES=16` | Pass |
| iOS UI build and launch | `dotnet build src/MuscleCuties.UI -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64`; clean rebuild; `simctl install/launch` | **0 warnings / 0 errors**; launched process stayed alive with no managed or AOT crash after rebuild |
| Nutrition presentation boundary | `UiContractGuardTests.Nutrition_presentation_does_not_reference_core_or_use_case_contracts`; full seven-path source scan | **0** contract references outside mapper |
| Registered-page reachability | `UiContractGuardTests.Registered_pages_have_an_entry_point_and_a_back_path` | Pass; Train now exposes Workout Archive |
| No empty route handlers | `ServerRouteParityTests.EveryMappedRouteHasAHandlerAndNonEmptyPattern` | Pass; every mapped route has a delegate and non-empty pattern |
| Route and query-key ownership | `UiContractGuardTests.Route_formats_and_query_keys_live_only_in_a_navigation_contract` | Pass |
| Visible copy is catalogued | `UiContractGuardTests.Page_and_shell_xaml_carry_no_literal_visible_text` | Pass |
| Accessibility copy is catalogued | `UiContractGuardTests.Accessibility_hints_and_descriptions_use_localized_or_bound_values` | Pass |
| EN/RU key resolution and parity | `LocalizationCatalogTests` in the Core project | Pass |
| Colour roles only | `UiContractGuardTests.Value_shaped_colour_tokens_are_definition_only` | Pass |
| Inline colour exceptions are semantic, not duplicate styling | `UiContractGuardTests.Inline_text_colours_match_the_documented_exception_list` | Pass, 20 documented sites |
| Dimensions use shared tokens | `UiContractGuardTests.Page_and_control_xaml_use_tokens_for_dimensions` and `UiStyleGateTests.Consumer_style_values_resolve_through_shared_tokens` | Pass |
| Deterministic style, duplication, dead-style and copy gate | `UiStyleGateTests` (4 facts): token-only consumer values, one definition per shared key/style, no orphaned styles, C# resource-key resolution, duplicate-free catalog copy, declared contrast pairs | **4/4**; injected `Padding="7"` failed at `AppStartupPage.xaml`, then restored SHA-256-identically |
| Two buttons, two cards, one shared style source | `UiCleanlinessAndReachabilityTests.Shared_styles_enforce_2_button_styles_2_card_styles_and_shared_surfaces` and `Visual_styles_are_defined_in_shared_styles_file` | Pass |
| Buttons/cards/inputs retain shared appearance | `Xaml_buttons_use_shared_styles_without_local_appearance_overrides`, `Canonical_cards_have_no_local_surface_overrides`, `All_inputs_use_shared_profile_setup_appearance` | Pass |
| Approved contrast floors | `Soft_minimal_palette_meets_approved_contrast_floors` and `UiStyleGateTests.Dictionary_contrast_pairs_meet_the_wcag_floor` | Pass; 22 declared semantic pairs |
| Dismissible sheets use the shared modal lifecycle | `Page_local_modal_chrome_lives_inside_the_shared_modal_control` and `Shared_modals_isolate_background_restore_focus_and_announce_a_heading` | Pass; only the documented non-dismissible day-overview exception remains |
| Architecture and async boundaries | `CleanArchitectureBoundaryTests` | Pass |
| No whitespace defects | `git diff --check` | Pass |

## Open items

### Deployed performance — instrumentation landed, gate re-measurement pending

The only deployed backend is `musclecuties-backend-api` in `us-east-2`, configured as **Staging** and bound to the staging database. On 2026-11-04 version 11 was deployed at **3008 MB** (from 1024 MB) with per-route request telemetry, and API Gateway detailed metrics were enabled. Public probes are green and warm handler time is 2–40 ms with init 1,046.74 ms. The 2026-11-03 measurement of p99 6,895.20 ms and warm p99 5,392.18 ms was taken without route attribution, so the historical tail is **not yet explained** and this gate stays **open** until a fresh window is measured.

RDS is demonstrably not the cause: over 14 days the instance held full CPU credit balance (288), peaked at 50.9 % CPU, and showed 30 ms maximum read latency.

The local all-route gate cannot stand in for this. Its heavy routes short-circuit — `strength-proposals` records 1.15 ms at HTTP 409 and the accept route 0.41 ms at HTTP 400 — so plan generation is never measured locally.

The owner action is to read the new per-route lines (or the detailed metrics) after real app traffic, then re-run `bash scripts/measure-deployed-latency.sh`. No production deployment exists, so production p99 cannot be claimed or measured yet.

### Owner device verification

The original owner rule reserved MAUI builds, runs and screenshots for the owner. The 2026-11-04 build/run/test pass was explicitly authorized; navigation, both appearances and screenshot-based checks remain owner evidence:

- Build and launch on iOS, including the iOS-only target evaluation.
- Both appearances, largest Dynamic Type, EN/RU expansion, keyboard/safe-area behavior and VoiceOver.
- Reduce Motion and rapid sheet reopen/close behavior.
- The new Train → Workout Archive entry and its back return, on device.
- Any visual contrast or layout outcome that depends on rendered pixels rather than source pairs.

No commit or push is part of this gate.
