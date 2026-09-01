# 10 — The content engine integration

Swarnakshi's UAT suite doubles as its demo material. Every run writes a **narration transcript** —
the same sentences shown on screen as captions and used to name the failing step when a scenario
breaks — with the timings needed to lay them over a recording. See
[08-uat](08-uat.md#the-narration-transcript) for the format and where the files land.

This document is the brief handed to the **Sivayaan Technologies content engine** (a separate
repository) to consume those transcripts. It lives here rather than there because the contract it
describes is ours: if the JSON shape, the journey list, the commands or the environment variables
change on this side, this file is what goes stale, and it should change in the same commit.

## Why a transcript rather than an AI-written script

The narration is reviewed prose that already serves two masters: it is what a viewer hears, and it is
what names the business step when a run fails. Regenerating a script from a recording would produce a
second description of the same journey, free to drift from the first. Reading the transcript keeps
one source of words for both purposes.

The engine keeps its existing AI path for every project that does not emit transcripts — this is an
additional source, not a replacement.

---

## The brief

Everything below is written to be pasted into an agent working in the content-engine repository.

## Task

Add **Swarnakshi** as a project in this content engine, and teach the engine to build its demo videos
from a narration transcript the source project now emits — falling back to the current AI-driven
process for any project that does not emit one.

Do **not** start coding until you have inspected this repository and understood how projects are
currently registered, configured, and turned into videos. Report what you find and what you propose
before changing anything.

## Part 1 — Understand what exists here

Before writing code, establish and tell me:

1. How a project is currently registered — a config file, a database row, a folder convention, a
   class per project? Where does the list of projects live?
2. What configuration a project carries today (repo URL, branding, voice, output format, publishing
   targets, schedule).
3. How the pipeline currently gets its narration/script: which AI engine, what prompt, what inputs it
   is given (screen recording? screenshots? a repo scan?), and where the script is stored between
   generation and rendering.
4. Where video assets enter the pipeline and what format is expected.
5. Whether there is already any notion of a project supplying its own script, and if so, reuse it
   rather than inventing a parallel path.

## Part 2 — Register Swarnakshi

**Swarnakshi** is a construction expense and inventory management system — a business operating
system for a builder running villa projects across multiple sites.

| Field | Value |
|---|---|
| Project name | Swarnakshi |
| Repository | https://github.com/yugandharsimhadri/Swarnakshi |
| Default branch | `main` |
| Stack | .NET 10 (Clean Architecture) + React 19 / Vite / Tailwind / Zustand |
| Domain | Construction: sites, villa projects, material master, inventory, procurement, approvals, project costing |
| Audience for demos | Builders, site engineers, construction business owners |

### Build prerequisites (the engine will need these to produce fresh recordings)

- .NET 10 SDK
- Node 20 (the suite starts the Vite client itself)
- Playwright Chromium — installed on demand by the suite; on a bare Linux runner install with deps
  first: `pwsh tests/Swarnakshi.UatTests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium`

### Commands

```bash
# Everything below runs from the repository root.

# The demo run: headed, paced, each narration beat drawn on screen as a caption.
SWARNAKSHI_UAT_RUN_MODE=demo dotnet test tests/Swarnakshi.UatTests -p:Uat=true

# One journey only (both viewports) — prefer this when recording a specific feature.
SWARNAKSHI_UAT_RUN_MODE=demo dotnet test tests/Swarnakshi.UatTests -p:Uat=true \
  --filter "FullyQualifiedName~PurchaseUatTests"
```

**`-p:Uat=true` is mandatory.** Without it the suite reports "no tests" and exits 0, which looks like
a pass. Always assert that the expected number of cases actually ran.

### Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `SWARNAKSHI_UAT_RUN_MODE` | `test` | `demo` = paced (220ms/action) + on-screen captions. **Use `demo` for recordings.** |
| `SWARNAKSHI_UAT_HEADED` | `true` (auto-`false` on CI) | Browser visibility. Must be `true` to record. |
| `SWARNAKSHI_UAT_VIEWPORT` | `desktop` | `desktop` or `mobile` — the suite drives both per journey regardless |
| `SWARNAKSHI_UAT_DESKTOP_SIZE` | `1440x900` | Recording frame size for desktop journeys |
| `SWARNAKSHI_UAT_MOBILE_DEVICE` | `iPhone 15 Pro` | Playwright device descriptor for mobile journeys |
| `SWARNAKSHI_UAT_BASE_URL` | `http://localhost:6070` | Client. **Not** the developer's 6050 |
| `SWARNAKSHI_UAT_API_BASE_URL` | `http://localhost:6071` | API. **Not** the developer's 6051 |

The suite starts its own API and client on 6070/6071 against a throwaway SQLite database and deletes
it afterwards. It never touches a running dev server or a developer's data. Nothing needs to be
running beforehand.

### Output paths (relative to the repository root)

| Path | Contents |
|---|---|
| `artifacts/uat/narration/<Journey>-<Viewport>.json` | **The narration transcript — the input this task is about** |
| `artifacts/uat/*.png` | Final-frame screenshot per journey; failure screenshots |
| `artifacts/uat/api.log`, `web.log` | Server output, for diagnosing a failed run |

`artifacts/` is gitignored — these are produced by a run, not committed. The content engine must run
the suite itself to obtain them.

### The 12 journeys

Each runs on **desktop and mobile**, so a full run yields 24 transcripts. `--filter
"FullyQualifiedName~<Test class>"` selects one.

| Key | Test class | Module | What it demonstrates |
|---|---|---|---|
| `SignIn` | `SignInUatTests` | Security | Sign out, wrong password refused, right one admitted |
| `Dashboard` | `DashboardUatTests` | Overview | Projects, sites, inventory value, receivable on one screen |
| `UserAccess` | `UserAccessUatTests` | Security | User administration and the Approval Centre are owner-only |
| `MaterialCatalogue` | `MaterialCatalogueUatTests` | Material Master | Search by name and by specification value |
| `AddMaterial` | `AddMaterialUatTests` | Material Master | Subcategory-driven specification fields |
| `MaterialLifecycle` | `MaterialLifecycleUatTests` | Material Master | Deactivate, find under Inactive, reactivate |
| `ContractorMaster` | `ContractorMasterUatTests` | Party Master | Add, find, deactivate preserving history |
| `CustomerMaster` | `CustomerMasterUatTests` | Party Master | Add, find, see what references the record |
| `PurchaseToConsumption` | `PurchaseUatTests` | Procurement | **Buy into a site, see it in that site's stock** |
| `MaterialRequestApproval` | `MaterialRequestUatTests` | Procurement | **Request → owner approval → issue** |
| `SiteInventory` | `SiteInventoryUatTests` | Inventory | Stock scoped per site, searchable |
| `Reports` | `ReportsUatTests` | Reporting | Standing reports render and export |

The two in bold are the product's core value story — money and stock actually moving. Lead a
general-purpose demo with those.

## Part 3 — The transcript format

One file per journey and viewport, e.g. `PurchaseToConsumption-Desktop.json`:

```json
{
  "key": "PurchaseToConsumption",
  "displayName": "Purchase Through To Project Cost",
  "module": "Procurement",
  "businessPurpose": "Buy material into a site, have the owner approve its release, issue it to a project, and see it become that project's cost — without the same rupee being counted twice.",
  "viewport": "Desktop",
  "runMode": "Demo",
  "recordedAt": "2026-09-01T18:17:45.065538+05:30",
  "durationMs": 13274,
  "succeeded": true,
  "cues": [
    { "index": 1, "startMs": 0,    "endMs": 1529,  "text": "[Procurement] Purchase Through To Project Cost", "isTitle": true },
    { "index": 2, "startMs": 1529, "endMs": 4217,  "text": "A purchase brings material into a site's stock — not into a project. Stock is held at the site and shared by every project on it.", "isTitle": false },
    { "index": 3, "startMs": 4217, "endMs": 6258,  "text": "The site receiving the stock and the supplier billing for it are chosen first.", "isTitle": false }
  ]
}
```

### Field semantics — read these before mapping

- **`cues[]`** is the script, already in order, already written in business language. It is not raw
  test output: these are the same sentences shown on screen as captions and used to name the failing
  step when a run breaks.
- **`endMs` is when the next cue replaced it, not a fixed duration.** A caption stays up while its
  step runs, so a slow step yields a long cue. Use the pair directly as subtitle in/out points — do
  not recompute durations.
- **Times are relative to the journey's title card** (`index: 1`, `isTitle: true`), which is the
  first thing on screen. They are *not* wall-clock. Sync the recording to that first frame.
- **`isTitle: true`** marks the chapter heading (`[Module] Journey Name`). Everything after is body.
- **`durationMs`** is the whole journey; the last cue's `endMs` is at or near it.
- **`succeeded: false`** means the journey broke. The transcript then ends at the step that failed —
  useful for diagnosis, but **never publish a failed journey**. Skip it and report it.
- Timings reflect the run that produced them. A `test`-mode run is far faster than `demo`, so its
  cues will be too tight for narration. **Only use transcripts where `runMode` is `Demo`.**

## Part 4 — What to change in the content engine

Add a **transcript-first** source of narration, with the existing AI process as the fallback.

Required behaviour:

1. **Per-project capability, not a global switch.** A project either supplies transcripts or it does
   not. Express this as project configuration (e.g. a `narrationSource` of `transcript` | `ai`, or a
   capability the project declares), so adding a second project that supplies transcripts needs no
   engine change.
2. **When a project supplies transcripts:** run its capture command, read the JSON, and build the
   script and subtitle track from `cues[]` directly. Do not send it to the AI engine to be rewritten.
   The text is deliberate — it is reviewed prose that doubles as the acceptance criteria.
3. **When a project does not, or the transcript is missing, empty, unparseable, or
   `succeeded: false`:** fall back to the current AI-driven process, unchanged. A project that works
   today must behave identically after this change.
4. **Log which path was taken** for every render, so it is never a mystery which video was scripted
   by whom.
5. **Optional AI on top, off by default:** allow the AI engine to *augment* transcript text — a
   hook, an intro, a call to action — but never to silently rewrite the cues.

Also handle:

- Transcripts are produced by a run, not committed. The engine must execute the capture command and
  read the output directory afterwards; do not expect files to exist in a fresh clone.
- Assert the run actually produced the expected number of transcripts. A `-p:Uat=true` omission
  yields zero tests and exit code 0, which must not be mistaken for success.
- Prefer selecting journeys by key rather than by file glob, so a partial run does not silently
  produce a shorter video than intended.

## Part 5 — Deliverables

1. A written summary of how projects are configured here today, before any change.
2. Swarnakshi registered with the configuration above.
3. The transcript-first narration path, with fallback, per project.
4. Tests: a project with transcripts uses them; a project without falls back; a malformed or failed
   transcript falls back rather than throwing.
5. A note in this repo's docs covering both paths and how to add the next transcript-supplying
   project.

Do not change how existing projects are rendered. Their output should be byte-identical after this
work unless they are explicitly opted in.
