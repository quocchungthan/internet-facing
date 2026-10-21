# 30s business-domain x technical-practice matrix

## Purpose and reading convention

30s helps people understand a car issue, make appropriate checks, and find a safe
remedy or service handoff. Exploration and 3D support that purpose; they do not
establish a diagnosis or substitute for verified service instructions.

**Columns are business capabilities in hierarchy. Rows are engineering
practices**, not `domain/application/presentation/rendering` implementation tiers.
Each cell identifies the responsible code component and what it does for that
business capability. Responsibility here means a module's responsibility, not
an assumed person or team assignment.

This is a source-grounded inventory, not a claim of complete Clean Architecture
compliance. Do not introduce a factory, interface, or file just to fill a cell.

## Source baseline and status

Reviewed on 2026-10-20 against target baseline `b5fbbe9` and feature implementation
commit `704eec6` on `feat/30s-component-workshop`. The workshop hierarchy, drawing
factory, wiper guide, complexity configuration and feature-quality workflow
are committed and included alongside this document for review.
The independent-feature follow-up on the same review branch adds build-time
composition, a no-3D shell and combination checks.

- **B**: present in the committed baseline.
- **P**: feature implementation included in this PR, not yet integrated into
  the target baseline.
- **Gap**: absent or not separated into the stated responsibility.

Paths below are relative to `web/` unless prefixed with `../`. **P** describes
review/integration status, not hypothetical or uncommitted code. Change these
labels when the implementation is integrated into the target baseline.

Source entry points: [catalog](src/domain/catalog.ts),
[exploration hierarchy](src/features/exploration/domain/items.ts),
[drawing factory](src/features/exploration/rendering/drawings/DrawingFactory.tsx),
[repair graph](src/features/repair/domain/wiperGuide.ts), and
[learning panel](src/components/DetailPanel.tsx).

## Business hierarchy used by the columns

```text
30s
  Vehicle knowledge
    Catalog
      Vehicle configuration / variant
    Exploration
      Vehicle > system > assembly / part occurrence
  Repair guidance
    Issue / symptom
      Observation > check > remedy > verification / stop
  Learning
    Part information
      Explanation / field book / reference photograph / source
```

These are capability paths for navigating this document, not four invented
domain aggregates. In the exploration implementation, the physical hierarchy
has explicit `Item` parent links. For example:

```text
vehicle
  powertrain-assembly > engine > engine-block / engine-head / engine-cover
                              > intake / lubrication
  chassis-assembly > wheels > wheel-front-left / other wheel occurrences
                   > brakes > individual brake occurrences
                   > suspension > individual strut occurrences
  electrical-assembly > electrical
  body-assembly > body > wipers > wiper-left / wiper-right
                               > arm / blade > rubber occurrence
```

The wiper line abbreviates the actual occurrence IDs: `wiper-arm-left`,
`wiper-blade-left`, and `wiper-rubber-left` (and corresponding right-side IDs).
The arm and blade are siblings under their side's wiper; rubber is under blade.
Repeated occurrences share specifications, never selection identity.

## Two-dimensional responsibility matrix

| Technical practice | Vehicle knowledge > Catalog > Configuration | Vehicle knowledge > Exploration > System / occurrence | Repair guidance > Issue > Check / remedy / verify | Learning > Part information > Explanation / evidence |
| --- | --- | --- | --- | --- |
| **Clean Architecture: dependency direction and boundaries** | **B:** `domain/catalog.ts` owns `Vehicle`, `Part` and catalog validation; `content/index.ts` loads JSON through that contract. **Gap:** domain contracts also contain URL selection and drawing metadata. | **P:** `features/exploration/domain/items.ts` and `subject.ts` own hierarchy and subject rules without React/Three.js imports. `application/useWorkshop.ts` coordinates selection; presentation and rendering consume the contracts. **Gap:** the application hook depends directly on React/browser APIs; `DrawingKind` is still in the domain. | **P:** `features/repair/domain/wiperGuide.ts` owns steps and legal transitions independently of React. `presentation/RepairGuide.tsx` calls it. `ExplorationWorkshop.tsx` optionally connects step targets to exploration; `ContentWorkshop.tsx` runs repair without that adapter. **Gap:** observation-history orchestration lives in the presentation component, not a separate application use case. | **B:** `components/DetailPanel.tsx` consumes `Vehicle`/`Part`, renders explanations and fetches books. `ReferencePhoto.tsx` renders attribution. **Gap:** book loading and presentation share one component; no separate learning application boundary exists. |
| **Factory pattern: construction and dispatch** | **B:** `parseVehicle` validates loaded configuration; it is a parser, not a factory hierarchy. No catalog factory abstraction is needed merely for symmetry. | **P:** `createVehicleItems` constructs validated occurrences/specifications. `rendering/drawings/DrawingFactory.tsx` dispatches typed drawing kinds to geometry components; `CarModel.tsx` supplies occurrence identity and positions. The rendering factory owns geometry, not business decisions. | **P:** `wiperSteps` is a declarative transition graph, not a repair factory. No guide-factory abstraction currently exists or is required for the single implemented issue. | **B:** `DetailPanel` and `ReferencePhoto` directly render their content. No learning factory is present; the drawing factory must not become a dispatcher for books or repair rules. |
| **Workflow/state ownership and reuse** | **B:** `readSelection` resolves requested car/part IDs and returns invalid-link status. **P:** `useWorkshop.selectCar` resets subject state and transient viewing settings on vehicle change; `ContentWorkshop.useCatalogSelection` reuses `readSelection` for non-3D URL/history state. | **P:** `subject.ts` reads/writes shareable selection and isolation; `useWorkshop` coordinates navigation/history. `SubjectCamera.tsx` and `subjectBounds.ts` use the same camera for the whole vehicle and isolated descendants, rather than one controls implementation per part. | **P:** `advanceWiperGuide` validates a selected transition. `RepairGuide` owns the reversible in-memory observation trail. `ExplorationWorkshop.locateRepair` adapts step targets to the camera; optional `onLocate` lets the same guide run without 3D. There is no persisted diagnosis/service record. | **B:** `DetailPanel` owns tabs, slides and book-loading state. **P:** `useWorkshop` owns tour navigation; `ContentWorkshop` supplies part selection and books without exploration. Tour callbacks are optional for this standalone use. |
| **Independent environment-controlled composition** | **P:** `config/featureFlags.ts` validates four strict booleans; `vite.config.ts` rejects invalid build settings. Shared catalog data remains available to any enabled capability. | **P:** `App.tsx` mounts `ExplorationWorkshop` only for `VITE_FEATURE_EXPLORATION=true`; the scene and its controls never mount otherwise. Automatic chunk splitting prevents shared React code from pulling in Three.js. | **P:** `VITE_FEATURE_REPAIR` controls lazy guide mounting and entry points in both shells. Guide transitions do not depend on exploration; location controls are offered only when a real adapter is present. | **P:** `VITE_FEATURE_LEARNING` controls lazy `DetailPanel` mounting, tour and book entry points. `ContentWorkshop` provides independent HTML navigation. `tests/featureFlags.spec.ts` exercises all sixteen actual builds (including `VITE_FEATURE_ONBOARDING`), including all-disabled. |
| **Validation and explicit failure** | **B:** `vehicleSchema` and `parseVehicle` reject malformed data, duplicates and dangling part/source/photo references. `readSelection` exposes invalid URLs; the UI displays a notice rather than claiming that link succeeded. | **P:** `validateItems` rejects duplicate IDs, missing specifications/parents, multiple roots and cycles. `subtreeIds` rejects unknown subjects; `DrawingFactory` throws for unregistered kinds. | **P:** `advanceWiperGuide` rejects invalid choices/next steps. `wiperGuide.test.ts` checks that every step target exists on both vehicles. Uncertain fitment or unsafe access routes to service/stop rather than an invented successful repair. | **B:** `DetailPanel` shows failed book requests with `role="alert"` and explicitly discloses missing verified photographs. Catalog validation checks reference integrity, not the truth of external claims. |
| **Low cognitive complexity and cohesive functions** | **B:** `parseVehicle` uses named reference/uniqueness checks. **P:** `eslint.config.js` applies SonarJS cognitive complexity **<=15 per function** to source and tests. This is a configured limit, not a measured result in this document. | **P:** hierarchy selectors, bounds calculations, camera handling and geometry drawings are separate responsibilities. Typed factory lookup avoids a growing drawing dispatch conditional. The same complexity rule covers this slice; factory use alone does not prove simplicity. | **P:** declarative `wiperSteps` keeps scenario branching out of nested UI conditionals; `advanceWiperGuide` checks a single transition. `RepairGuide` renders the current step and trail. The same complexity rule applies. | **B:** `DetailPanel` still combines fetch, tabs, lessons and source presentation; this is an architectural concentration, not evidence that decomposition is complete. **P:** the complexity rule covers it too; do not add a legacy exemption. |
| **Focused tests and observable review evidence** | **B:** `domain/catalog.test.ts` checks catalog integrity; `tests/workshop.spec.ts` exercises existing workshop behavior. Tests cannot verify actual vehicle identity from a schema. | **P:** `items.test.ts`, `subject.test.ts`, `subjectBounds.test.ts`, `tests/components.spec.ts` and `tests/visual.spec.ts` cover identity, invalid trees, selection/history, camera fitting, interaction and reviewed screenshots. | **P:** `wiperGuide.test.ts` checks cleaning, replacement/service/stop transitions and item targets. `tests/repair.spec.ts` covers guide-to-3D behavior, reversible observations and constrained viewports. | **B:** `tests/workshop.spec.ts` includes learning/book interaction. **P:** feature browser/visual scenarios complement it; they do not verify the factual correctness of service instructions. |
| **Provenance, applicability and safety** | **B:** vehicle JSON records year, market, configuration facts, source scope and photo caveats. `parseVehicle` enforces source references; it does not establish OEM applicability. | **P:** `Item` identifies occurrences while `ItemSpecification` shares drawing identity. Drawings remain generic schematics; visible geometry must not be presented as verified dimensions, connectors or repair-grade assemblies. | **P:** `wiperReference` (strict `guideReferenceSchema` in `guideReference.ts`), step instructions and `RepairGuide` distinguish general wiper care from a model-specific defect. `resolveApplicability` labels guidance generic, unknown or verified (only for listed vehicles with a review under 365 days old); recall lookups are external official links only. Blade sizes, exact release procedures and diagnosis are not asserted. Unsafe access, uncertain fitment and persistent problems terminate at stop/service; verification records improved/unresolved/worse/not-tested outcomes. | **B:** `DetailPanel`, `ReferencePhoto`, catalog source metadata and `public/books/*.md` expose original explanations, licenses, source scope and variant mismatches. A reference photo must not silently become proof of the selected trim/engine. |

## Cross-cutting enforcement and open gaps

**Feature quality wiring:** `eslint.config.js` configures complexity;
`package.json` exposes lint/unit/build/browser commands;
`../.github/workflows/verify-30s.yml` runs them for `feat/30s-*` pushes
and relevant PRs into `sub/30s`. Reviewed image baselines are versioned;
CI uploads evidence and must not auto-approve changed screenshots.
This wiring is included in the PR, not proof that a remote check has passed or
branch protection is active.

**Operation boundary (contract v1, #48):** `src/application/contracts/operation.ts`
defines `defineOperation`/`runOperation`. Each operation declares a dotted
domain name, `query`/`mutation` kind, Zod input/output schemas and the domain
failures it may report (`invalid_argument`, `not_found`, `conflict`,
`unavailable`). Input is validated before execution; output is validated before
return. Undeclared failures and invalid output are `contract_violation`;
unexpected exceptions are `internal`. `AbortSignal` is execution context:
a pre-aborted call is `cancelled` without validation or execution, and abort
during execution is `cancelled` without any rollback claim. The module has no
React, browser, server or transport dependency and implies no persistence.
v1 compatibility: adding optional input fields or new operations is compatible;
removing/renaming fields, tightening input, or changing failure codes needs a new
operation version. Future HTTP/MCP/agent adapters call `runOperation` and map
results (e.g. to RFC 9457 problem details); authorization, confirmation and
idempotency for consequential writes belong in those adapters, not here.
First domain contracts (#49): `features/catalog/application/catalogOperations.ts`
(`catalog.listVehicles` returns the bounded `Vehicle[]` without pagination;
`catalog.getVehicle` returns `Vehicle | null` for a well-formed missing id) and
`features/repair/application/wiperOperations.ts` (`repair.wiper.advance` wraps
`advanceWiperGuide`; unavailable choices are `invalid_argument`). They wrap the
existing data/transition without presentation imports or persistence. The React
shells still call the domain directly; routing them through these contracts is
not yet done and needs no behavior change when it is.

The following limitations must stay visible rather than being filled with
aspirational component names:

- Strict framework-independent application boundaries are not yet established;
  browser navigation is in `useWorkshop`, and repair orchestration is in React.
- The exploration domain contains renderer-oriented `DrawingKind` metadata.
  Changing that dependency needs a separately scoped implementation decision.
- Learning content loading and presentation are coupled in `DetailPanel`.
- The repair slice is one general wiper-maintenance flow, not a verified
  model-specific known-fault catalog or a general diagnosis engine.
- Domain validation checks structural integrity; factual source review and
  variant-specific service verification remain separate responsibilities.

## Keeping this document current

When a change adds/removes a business capability, changes hierarchy or moves a
responsibility, update the affected column/cells **in the same PR**:

1. Record the source revision/date and whether the described implementation is
   in the target baseline, feature code awaiting integration, or a gap.
2. Name the actual module/symbol and its responsibility; do not list only a
   principle or an intended future component.
3. Preserve the two axes: hierarchical business capability columns and technical
   practice rows. Physical parts and implementation directories are supporting
   evidence, not replacements for either axis.
4. Update affected validation/test evidence and disclose remaining coupling or
   unsupported applicability. Do not claim compliance or passing checks from
   file presence alone.
5. Remove stale paths and change **P** to **B** only after the implementation is
   integrated into the baseline being documented.

This is a contributor maintenance rule, not an automatic synchronization tool.
No generator, dependency-boundary lint rule or CI drift detector is introduced
by this documentation change.
