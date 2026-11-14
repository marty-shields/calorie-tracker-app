# AGENTS.md — Calorie Tracker

Personal Android app (.NET 11 MAUI Blazor Hybrid). Setup: [docs/dev-setup-build.md](docs/dev-setup-build.md) ([Windows](docs/dev-setup-windows.md), [Linux](docs/dev-setup-linux.md)). E2E runs: [docs/e2e-tests.md](docs/e2e-tests.md). Release: [docs/release.md](docs/release.md). Screen behaviour: the design system's [screen pages](src/CalorieTracker.UI.Stories/Stories/Screens/) (BlazingStory). Work items and their acceptance criteria: GitHub issues.

## Commands
- Build: `dotnet build CalorieTracker.slnx` (warnings fail the build)
- Code style: `dotnet format CalorieTracker.slnx --verify-no-changes` (CI runs it; drop `--verify-no-changes` to apply the fixes)
- Test: `dotnet test tests/CalorieTracker.Core.Tests` and `dotnet test tests/CalorieTracker.UI.Tests`
- E2E (needs a device; a plain `dotnet test` skips them): build the App, then `dotnet test tests/CalorieTracker.E2E.Tests --filter "Category=smoke"`
- E2E without a device (Linux): `.github/scripts/e2e-local.sh "<filter for what you changed>"` boots an emulator, runs the filter, tears it down; exit 2 means skipped, so say the tests did not run ([details](docs/dev-setup-linux.md#throwaway-emulator-for-agents)). The full suite is CI-only (1 shard for now); one emulator can take a long time as the suite grows. The script refuses a broad filter, more than 15 scenarios or a run over 10 minutes; `E2E_ALLOW_BROAD=1` is the deliberate opt-in.
- Red emulator shard: read the job summary's `infra` or `test` verdict (`classify-shard-failure.sh`). Rerun the failed job once, and only on `infra`. On `test`, or when the rerun fails too, fix the cause instead ([rule](docs/e2e-tests.md#a-red-emulator-shard)).
- Story layout (needs Chrome and the stories running): `node .github/scripts/check-story-layout.mjs http://localhost:5056 <chrome>` fails on clipped, overflowing or wrapping story content at the narrowest card; CI runs it as `story-layout`, and `data-wrap-ok` marks text allowed to wrap.
- README screenshots (needs Chrome and the stories running): `node .github/scripts/readme-screenshots.mjs http://localhost:5056 <chrome>` retakes every screenshot in the [README table](docs/dev-setup-build.md#readme-screenshots), light and dark; `--check` fails on any that no longer match their story. CI runs the check in `story-layout` and uploads retaken images as the `readme-screenshots` artifact when it fails.
- CI lint (needs `shellcheck`): `.github/scripts/lint-ci.sh` runs shellcheck over the scripts and actionlint over the workflows; CI runs it in `dev / build-and-test`.
- Docs: `python3 -I .github/scripts/check-docs.py` fails on a relative link or `#anchor` in a tracked `*.md` that doesn't resolve, or a README screenshot and its table row that don't match; CI runs it in `dev / build-and-test`.
- Food names: `python3 -I .github/scripts/check-food-names.py` fails on a seed food with no everyday name, a duplicate, an overlay that doesn't match the seed, or a name that breaks the [naming guide](src/CalorieTracker.Core/Seed/naming-guide.md); `--base <seed.json>` also fails on a changed `sourceId` or `originalName` (CI passes the base branch's seed on pull requests; by hand: `git show origin/main:src/CalorieTracker.Core/Seed/cofid-foods.json > /tmp/base.json`); CI runs it in `dev / build-and-test`.
- Design system: `dotnet run --project src/CalorieTracker.UI.Stories` → http://localhost:5056

## How the code fits
- Projects: App → UI → Core; Stories → UI. `CalorieTracker.UI.Tests` reads the App's files because it can't reference it.
- Screens: a `*Page` calls services, a `*View` takes parameters. `StoryCoverageTests` requires a screen story for every `*View`.
- Services return a status (such as `ReadResult<T>` or a result record), not exceptions.
- Every database call goes through `DatabaseGate` (`DatabaseGateTests`).
- Event ids: `CoreEvent` 1000s, `UiEvent` 2000s, `AppEvent` 5000s. `CoreEventTests` and `EventIdTests` fix the values and ranges.
- Styling: tokens only, no literal colours or sizes (`TokensOnlyCssTests`).
- Logs hold no PII: ids, counts, events and non-personal data such as a scanned barcode are fine, personal data never is (obfuscate when needed) (`Logging.feature`). Add a test that a new feature's log lines carry no PII.
- Docs: `ReadmeDocsTests` checks the README's screenshot table and Project layout.
- Upgrade check: `Upgrade.feature` and `UpgradeSeed.feature` save and check what the last release stored (just the theme so far); the upgrade jobs skip until a `v*` tag exists.
- Skeleton: the app has no features yet. A new screen needs a `*Page`, a `*View`, a story per state, a docs page under `Stories/Screens/`, an entry in `ScreenStates` and its E2E scenarios.

## Rules
- Ask about any ambiguity.
- No new packages without approval.
- XML comments only when they add something the name and type don't (a unit, a range, what null means, an order, a why). Component parameters are the exception. Never restate the name. Build warnings no longer ask for them, and CI's `check-filler-comments.py` fails a `<summary>` or `<param>` that only restates the name (a real exception gets `/// <!-- filler-ok: why -->`).
- Interfaces only where something else implements them, such as a test fake or the stories site. No interface for a single implementation nothing swaps.
- Commit on a branch, push it and open a ready (non-draft) PR when the work is done, so all CI runs (a draft skips the heavy jobs). Merge only when the user says to.
- PR titles are Conventional Commits with exactly one `semver:major|minor|patch` label (CI checks both).
- A PR that changes the UI includes screenshots of the changed screens, posted as a PR comment or in the body, before it is marked ready.
- A PR that changes how a screen in the README looks (its markup, CSS, tokens, or the sample data its story shows) retakes the README screenshots with `readme-screenshots.mjs` and commits them in the same PR. Run `--check` before marking it ready; never leave them stale for later.
