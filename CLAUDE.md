# MuscleCuties

## Project

Cycle-synced workout and nutrition app for women. Adjusts recommendations based on menstrual cycle phase, user goals, and symptoms. Built with .NET MAUI, EF Core + SQLite, CommunityToolkit.Mvvm, strict MVVM architecture. Recommendations are locally generated (rule-based by cycle phase), not AI/LLM. Keep everything MVP-clean -- no over-engineering, no speculative abstractions.

## Workflow Rules

- Never use emojis in code, tests, comments, XAML, or any file written to disk
- Never mention Claude, AI tooling, superpowers, or "Co-Authored-By" in commit messages. Write commits as a developer would
- Never commit without explicit user approval. Present changes for review first, commit only after the user says so
- Never auto-run `dotnet build` or `dotnet test`. Present the command and wait for the user to run it and paste output
- No pink BoxView dot on Auth page (Login, Register) brand marks -- wordmark and tagline only
- All commentary and explanations belong in chat, not in code comments

## Build & Test

Present these commands for the user to run -- do not execute them:

```
dotnet build src/MuscleCuties.App/MuscleCuties.App.csproj
dotnet build src/MuscleCuties.Core/MuscleCuties.Core.csproj
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

## Code Conventions

### General

- 4-space indent for .cs, .xaml, .csproj. 2-space for .json, .md
- UTF-8, LF line endings, final newline
- System using directives sorted first
- Nullable reference types enabled globally

### MVVM Layer Boundaries

- **Models** (`MuscleCuties.Core/Models/`): Pure data. No CommunityToolkit.Mvvm references. No UI state
- **ViewModels** (`MuscleCuties.Core/ViewModels/`): UI state and commands via CommunityToolkit.Mvvm. Organized by feature (Auth, Cycle, Dashboard, Nutrition, Profile, Quiz, Workout)
- **Views** (`MuscleCuties.App/Pages/`, `Controls/`): XAML + thin code-behind. No business logic
- **Services** (`MuscleCuties.Core/Services/`): Business logic. Organized by feature domain

### MAUI / XAML

- Use `StaticResource` over `DynamicResource` unless the resource genuinely changes at runtime
- Use `x:DataType` on every XAML root for compiled bindings
- Prefer `Path` with SVG data over `Image` + `FontImageSource` converter for icons in modals and conditionally-visible containers (FontImageSource can fail to render in visibility-toggled parents)
- Scope resource dictionaries to the pages that need them

### Model Design Patterns

- `QuizAnswer.MappedValue`: typed int convention for mapping quiz answers to domain values
- `SelectableQuizAnswer`: UI selection wrapper lives in ViewModel layer, never on the model
- Feature enums (Goal, DietaryPreference, ExperienceLevel, etc.) live in `Models/Enums/`

## Performance

Standing principles for MAUI performance work:

- Flatten nested XAML layouts -- fewer visual tree elements means faster measure/arrange passes
- Defer heavy controls (modals, secondary tabs) until needed. Use lazy instantiation, not pre-built hidden views
- Use skeleton placeholders for async-loaded content
- Batch off-thread population of dynamic lists
- Prefer singleton pages for tab shells to avoid repeated construction
- Split app preload into stages to reduce time-to-interactive
- Profile before optimizing. Measure startup, tab switch, and scroll frame times

## Agent Pipeline

For performance optimization tasks, run these agents in order. Each agent's `.md` file in `.claude/agents/` carries the full detail.

1. **xaml-visual-tree-optimizer** -- flatten layouts, reduce element count. Highest impact, lowest risk. Run first
2. **maui-lazy-loading-agent** -- defer heavy controls, skeleton placeholders. Run after layout flattening
3. **maui-bindings-resources-agent** -- add compiled bindings, scope resources, StaticResource over DynamicResource. Run after lazy-loading
4. **maui-build-startup-agent** -- AOT/trimming/ReadyToRun settings, startup sequencing. Run last before review (needs Release-mode device verification)
5. **maui-performance-reviewer** -- verify no regressions. Run after all other agents

## Architecture Reference

- `docs/PROJECT_ARCHITECTURE.md` -- overall project structure
- `docs/WORKOUT_PLAN_ARCHITECTURE.md` -- workout plan system design
- `docs/PLANNING_CUSTOMIZATION_GUIDE.md` -- plan customization approach
- `.editorconfig` -- code style rules (enforced by tooling)