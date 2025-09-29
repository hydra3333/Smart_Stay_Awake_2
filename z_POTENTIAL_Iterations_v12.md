Here’s a **draft, evolving iteration plan** for next steps focused on **tray/menu integration** that
mirrors the *behaviors* of the example Python program, while aligning with your v11 spec, coding rules, and the current C# solution layout.

> **Assumptions / references**
>
> * **(1) Example Python program**: we’ll treat it as the behavioral reference for the tray/menu (menu items, state machine, and outcomes).
> * **(2) “v11 change spec”**: the source of truth for safety-first rules, coding style, comments, and tracing requirements (even if slightly out of date).
> * **(3) Repo**: `github.com/hydra3333/Smart_Stay_Awake_2` with C# sources under `/src`.
>
>   * **Do not fetch or open** `EmbeddedImage.cs` (huge base64). We’ll avoid downloading/reading that file; where needed we’ll stub interfaces.
> * **(4) Current code**: we keep existing function/variable names and comments wherever reasonable.
> * **(5) Naming stability**: no renames unless there’s a compelling defect/safety reason; if so, we’ll propose a minimal-impact change with migration notes.
> * **(6) Goal for this track**: menu parity (behaviors and UX outcomes) with the Python app—not line-for-line translation.

---

# Draft plan (will evolve)

## Iteration 0 — Prep & alignment (no code shipped)

**Objectives**
* Extract the **menu behavior contract** from the Python program: item list, enable/disable rules, toggles, shortcuts, and what each action *does* (end state and user feedback).
* Map each behavior to **v11 safety-first** requirements (fail-safe defaults, guardrails, user confirmations where needed).
* Lock **trace vocabulary** and **log levels** (e.g., `TRACE: Menu/Click`, `TRACE: Tray/State`, `TRACE: Power/KeepAwake`, `TRACE: Timer/Schedule`, `TRACE: UI/Toast`).

**Artifacts**
* One-page **Menu Behavior Matrix** (item → preconditions → action → postconditions → user-visible result → safety notes → trace lines).
* **Tracing style guide**: naming, correlation IDs, minimal PII, sample lines.
* **Risk register** for tray/menu (e.g., duplicate hooks, multi-instance, suspended session).

**Success criteria**
* Stakeholder sign-off on matrix + tracing guide.
* Explicit agreement to **not open `EmbeddedImage.cs`** in dev tools.

---

## Iteration 1 — Tray icon lifecycle & safety boilerplate

**Objectives**
* Implement/validate the **tray icon** lifecycle (create once, survive window close, dispose cleanly on app exit; single-instance guard).
* Add **safety-first scaffolding**:
  * Catch-all exception boundaries around tray/menu callbacks.
  * Default to **do nothing dangerous** on error; display a safe toast/dialog; log a high-signal trace.
  * Ensure operations that modify system state are **idempotent** and **re-entrant guarded**.

**Tracing**
* `Entered/Exiting` for tray setup/teardown.
* App domain unhandled exception → trace and safe shutdown.

**Success criteria**
* No leaks on repeated show/hide of menus.
* Verified single instance and clean disposal.

---

## Iteration 2 — Menu model & state sync (behavior parity scaffold)

**Objectives**
* Introduce a **menu model** that reflects the Python app’s behaviors:
  * Items, separators, checkable/disabled states, and dynamic labels (e.g., “Keep Awake: On/Off”).
  * A **single source of truth** (model → render) so states don’t drift.
* Bind the model to **current app state** (e.g., active-timer, mode).
* Implement **render diff**: update only changed menu items (reduces flicker/risk).

**Tracing**
* On state change: `MenuModel.Update` with before/after summary (compact).
* On render: `Menu.Render` with item counts and flags.

**Success criteria**
* Visual menu parity with Python (look/structure), even if handlers are placeholders.
* Toggling an in-memory flag updates checks/enables predictably.

---

## Iteration 3 — Wire core actions to outcomes (no UI redesign)

**Objectives**
* For each menu item in the matrix, wire **the action to the expected outcome** (the *effect*, not Python code). Examples (illustrative):
  * Start/stop “keep awake” (e.g., preventing display sleep or system idle).
  * Quick duration presets (15/30/60 min) using a single scheduler source.
  * “Open window” / “Show status” / “Quit”.
* Ensure **mutual exclusivity** / precedence rules mirror the Python behavior.

**Safety**
* Each action guarded by try/catch + **rollback strategy** (e.g., if enabling keep-awake fails, revert UI state; log error; notify user).
* Validate inputs (no negative durations; clamp to allowed max from v11).

**Tracing**
* One-line **intent** (menu → action), one-line **result** (ok/error + parameter pack).
* Record **effective parameters** after clamping.

**Success criteria**
* Manual test paths match the matrix postconditions.
* Error injection (simulated failure) leaves the app in a safe, known state.

---

## Iteration 4 — UX polish in tray context

**Objectives**
* Add **tooltips**, **status badges** (if supported), and context cues (checked items, ellipses for dialogs).
* Non-blocking **toasts/notifications** for starts/stops/timeouts.
* Respect **system theme** and accessibility (high contrast, keyboard access via accelerators).

**Tracing**
* Entry points from keyboard accelerators vs mouse.
* Notification lifecycle (shown/hidden; coalesced IDs).

**Success criteria**
* Matches Python’s “feel” for feedback timing and clarity.
* No modal deadlocks; ESC/Alt+F4 behaves as expected.

---

## Iteration 5 — Robust scheduling & overlap rules

**Objectives**
* Centralize timers/schedules to **one scheduler** to avoid double-fires.
* Define **overlap policy** (e.g., starting a new duration cancels the previous).
* Persist last selection if that’s part of the Python behavior (optional; confirm with v11).

**Safety**
* Timer disposal is deterministic; rescheduling is atomic.
* App suspend/resume: re-evaluate timers safely.

**Tracing**
* `Timer/Set`, `Timer/Cancel`, `Timer/Fire` with correlation IDs.
* Suspend/resume events and reconciliation.

**Success criteria**
* No phantom timers; accurate timeouts; resilient to clock changes.

---

## Iteration 6 — Config & v11 conformance pass

**Objectives**
* Align with **v11 coding rules** (doc headers, comment density, guard-clauses, error messaging tone).
* Move tray behaviors’ tunables to **AppConfig** (e.g., default durations, maximums).
* **DO NOT** load `EmbeddedImage.cs`; rely on the existing fallback/icon surfaces already in the repo.

**Tracing**
* Single banner line at startup summarizing effective config (redact sensitive values).

**Success criteria**
* Style/static analysis green; comments meet “lots of comments” bar.
* Config changes affect menu correctly without restarts (if intended).

---

## Iteration 7 — Diagnostics, tracing quality & log UX

**Objectives**
* Ensure **trace categories** are filterable (e.g., `TraceSource` per area or consistent prefixes).
* Add **context IDs** (session/run/menu-click) to correlate flows.
* Optional: user-accessible **“Copy recent log”** action for support.

**Success criteria**
* Given a user report “menu felt laggy at 14:02”, logs clearly show action→result with timings.
* No PII in logs; sizes remain sane.

---

## Iteration 8 — Hardening & edge cases

**Objectives**
* Multi-monitor / DPI scaling changes while tray is active.
* Locked session, Fast User Switching, Remote Desktop.
* System power state transitions (sleep/hibernate) and resume.

**Success criteria**
* Predictable behavior; no crashes; actions no-op safely when platform APIs reject them.

---

## Iteration 9 — Documentation & release notes

**Objectives**
* Update `README` (high-level) and `docs/` (dev notes) with:
  * Menu matrix, safety notes, tracing keys, common flows.
  * How to test without `EmbeddedImage.cs` (build/run instructions).

**Success criteria**
* Another dev can reproduce behaviors and read traces without handholding.

---

## Iteration 10 — Post-merge tidy & backlog grooming

**Objectives**
* Review any remaining drift vs Python behavior.
* Collect UX feedback; decide whether to proceed to **bottom controls** track next.

---

## Cross-cutting rules (apply to all iterations)

* **Safety-first**: when in doubt, prefer “do nothing and inform” over risky action. Wrap any system-affecting call with guardrails and friendly error messaging.
* **Excellent tracing & comments**:
  * Every public method: summary of *why it exists* and *what it guarantees*.
  * Every menu action: intent + parameters + outcome (+ duration).
  * Keep logs structured (consistent prefixes/fields) and readable.
* **No gratuitous renames**: keep function/variable names and comments intact, unless a correctness/safety issue demands change; if so, propose minimal diffs with rationale.
* **Repository hygiene**: never open or fetch `EmbeddedImage.cs`. Stub or mock any code path that would read it in tests.
* **Parity over translation**: replicate **outcomes** of the Python menu, not its implementation details.

---

## Inputs we’ll still need (to lock details)

* The **Python menu reference** (list of items & behaviors).
* The **v11 change spec** (for exact coding/trace rules and safety language).
* Any **platform constraints** (Windows version targets, required APIs).

---

