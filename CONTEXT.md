# MuscleCuties context

## Localization

- **Localization catalog** — `UiTextCatalog`, the single source of authored English and Russian copy.
- **Authored key** — a descriptive identifier such as `Tab_Today`; resolve it with `Get` or `Format`.
- **Runtime text** — English text from models, notifications, converters or composed strings; resolve it with `TranslateRuntimeText`.
- **Runtime map** — the 1,395-entry English-to-Russian table in `UiTextCatalog.RuntimeText.cs`, retained from the retired hash-key catalog.
- **Hash key** — a retired `T` + ten hex identifier; no new hash keys are allowed.

Runtime text resolves through the runtime map, then the authored English reverse lookup, then the 79 format patterns, then the English text unchanged. `Get` never consults runtime data, and runtime text never receives a catalog key.
