# Workshop design contract (DES01)

This records the implemented workshop, not a redesign or proof of accessibility.
Baseline: `c7f51768a80e99948e7d6c43e89eb7335d50af5b`, integrated by #37.
The question action added for #52 is an intentional bounded addition within
selected-subject controls, also available in the HTML-only learning path.

## Regions and placement

| Region | Existing owner / layout contract |
| --- | --- |
| Main schematic | `ExplorationWorkshop` owns one `.scene` / `WorkshopScene` canvas. Full viewport, demand rendering; no per-component canvas. A desktop detail panel reserves 330px at the right. |
| Identity / picker | `.top-bar`, 28px top and 34px side desktop inset. Brand, current-car picker and share/help actions remain here. Picker changes canonical configuration, not physical-vehicle identity. |
| Tool rail | `.side-rail`, 30px left, vertically centred desktop. Repair/log, index, panel hinge, labels, capture and optional field book remain individually reachable. |
| Camera | `.camera-controls`, right 31px / bottom 126px desktop. Orbit left/right, zoom in/out and reset provide drag/scroll alternatives. |
| Footer | `.bottom-bar`, 30px bottom / 34px side desktop. Gesture hint, one view-mode group and educational disclosure. |
| Selected subject | `.subject-controls`, top 112px / left 100px, width 270px, max-height 45%, internally scrollable. Identity, focus/isolate/restore, question action, ancestry, children and connections reuse the canonical ItemTree. |
| Learning details | `.detail-panel`, top 108px / bottom 102px / right 28px, width 368px. Fixed identity/tabs and independently scrolling content. Selected engine children have their own lesson; missing content is explicit. |
| Help / repair / observations | Existing `.help-panel` and `.repair-guide` overlay positions and internal scrolling. They suppress conflicting subject/detail content, not core navigation or safety disclosures. |
| First visit / question editor | Native modal dialogs with labelled content, bounded viewport height and scrolling, visible Close/Skip and Escape handling. These are separate from the non-modal selected-subject region. |
| HTML-only shell | `ContentWorkshop`: scrolling document layout, max content width 1100px, native car/part selectors and independent repair/learning regions. No fake canvas dependency. |

At the existing **760px** breakpoint, rails/header/footer become compact; details
move to a bottom sheet and the subject region remains above it. Landscape below
560px height uses a narrower right-side detail panel. Preserve 375x812, 740x390
and 1440x900 regression coverage. Do not claim these prove every device.

## Source-derived visual conventions

`src/styles.css` supplies DM Sans body text and Space Grotesk headings/eyebrows.
Default root colors: background `#101716`, surface `#19211e`, ink `#eaf0e6`,
muted `#acb6aa`, border `#b3c9ad26`. Panels use the existing dark translucent
`.glass` surface, thin border, blur and shadow. Many component colors are still
literal values; changing root variables alone is not complete light-mode support.

`vehicleThemeStyle` derives accent/hover/soft/border/ink from the selected
catalog paint and lifts accent luminance to a 4.5:1 ratio against the dark base.
Do not assume that this proves contrast against every composed translucent
surface. Body identity, warnings and sources must not depend on accent alone.

Desktop primary controls are at least 44px tall; icon buttons are 42px square.
Camera controls and compact mobile/subject buttons can be 32-36px, and compact
landscape rail buttons are smaller still. These are **existing gaps against a
44px preferred touch target**, not permission to call all controls accessible.
Review any expansion against actual adjacent hit regions before making a
separately scoped size/layout change.

Use existing rounded panels/buttons, compact eyebrows, thin outlined controls,
visible native semantics and `:focus-visible` accent outline. Avoid a new design
library, replacement typography, competing sidebar, new route system or a
palette rewrite as incidental backlog implementation.

## Bounded additions and interaction criteria

The planned coffee-support and missing-car-request actions are separate adjacent
actions: support cannot be a prerequisite for a free request or promise priority.
No destination/provider/payment flow is implied by this design contract.

The planned selected-subject expansion is a non-modal disclosure using
`aria-expanded` / `aria-controls`, retaining compact identity and essential
focus/isolate/restore actions. Expanded content contains available hierarchy and
detail actions. Collapsing focused content returns focus to the disclosure.
Exactly one mode group belongs beneath visible subject controls, or in the
existing footer for whole-car/overlay states; never duplicate the group.
These are accepted design constraints, **not implemented UX01 behavior**.

New actions must preserve canonical IDs, selection, isolation, camera, history,
flags, field books and honest missing-content messages. Require keyboard-only
entry/exit, focus restoration, visible errors, bounded internal scrolling,
no rail/footer overlap and no hover-only action. Normal text needs measured
4.5:1 contrast; large text and meaningful control/focus boundaries need applicable
3:1 contrast. Check composed colors, not token names. Status must have text or
semantics, not color alone. Preserve native forced-colors behavior and reduced
motion; decorative effects are not a reason to run the canvas continuously.

## Screenshot and review provenance

The versioned comparison baselines are in `tests/visual.spec.ts-snapshots` and
`tests/repair.spec.ts-snapshots`: separate Windows and Linux Chromium baselines,
headless ANGLE/SwiftShader,
1440x900, reduced motion, blocked remote fonts and explicit Arial override.
They compare both whole cars, both isolated engines and the wiper guide at
0.1% maximum differing pixels. Windows feature-quality checks and Ubuntu
deployment checks select their own platform-suffixed files. Review intentional
UI changes on both platforms; neither platform's images replace the other's.
The Linux engine baselines were reviewed using Playwright 1.64.1's Noble Docker
image (Chromium 156.0.8078.4) after reproducing the deployment failure. Differences
were confined to the new question button and shifted subject hierarchy.

Focused captures from the 2026-10-22 run are retained in the session evidence
directory linked in the GitHub completion note, including desktop/portrait/
landscape selected-subject and question states, help/index/repair states, and
the pre-change selected-piston view. Browser version is recorded by the capture
runner. Those captures use the actual available web fonts unless specified;
they are review evidence, not interchangeable screenshot baselines.

For #52 the two inspected engine screenshot diffs change only the selected
control region (new question button plus shifted scrollable hierarchy).
Whole-car and wiper baselines must remain unchanged. Human product/design
acceptance remains distinct from automated screenshot comparison and the
implementation author's visual inspection.
