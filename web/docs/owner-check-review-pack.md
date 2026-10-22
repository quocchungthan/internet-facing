# Owner-check source and review pack

**Version:** 1, 2026-10-22. **DRAFT / NOT APPROVED.**
Preparation artifacts for #44, #45 and #47; handoff to #51. This document is not
an owner procedure, diagnosis, permission to drive or qualified safety approval.
It is not imported by the application. No new maintenance flow is enabled.

## Evidence inventory

Sources were retrieved on 2026-10-22. The summaries below are independently
written; no source artwork or procedures are reproduced.

| ID | Source / locator | Supports | Applicability and unresolved limits |
| --- | --- | --- | --- |
| HC | [GOV.UK Highway Code, Annex 6](https://www.gov.uk/guidance/the-highway-code/annex-6-vehicle-maintenance-safety-and-security), Warning displays; Tyres; Tyre pressures; Other problems; Overheated engines or fire | Manufacturer-specific pressure data, cold/hot distinction, warning context, visibility and serious hazards | UK road-use guidance, not global law or a Raize/Impala manual. Do not copy UK numerical/legal limits into universal guidance. Some generic diagnostic/procedural language exceeds this pack's scope. |
| AA | [AA dashboard warning lights](https://www.theaa.com/breakdown-cover/advice/dashboard-warning-lights), Engine management; Battery; Oil pressure; Brake | Meaning depends on the symbol, operating state and vehicle; serious warning combinations need professional assessment | UK roadside advice, not exact OEM instructions. Its conditional drive/restart suggestions are NOT adopted here. Braking/steering safety cannot be established from warning colour alone. |
| AD | [ADAC 2025 roadside-assistance balance](https://www.adac.de/der-adac/verein/corporate-news/pannenhilfebilanz-2025/), most frequent cause | ADAC attributes 45.4% of its 2025 roadside-assistance deployments to car-battery problems | German assistance population, not all vehicles, all repair visits, local model failure rates or a diagnosis. The 2026 statistics article linked in #44 returned only introductory text; this directly accessible ADAC primary report verifies the statistic. |
| W | [Existing AAA wiper source](https://www.acg.aaa.com/connect/blogs/4c/auto/windshield-wipers-safety-maintenance), referenced by `wiperGuide.ts` | Existing generic-wiper provenance pointer for reviewer follow-up | Not newly independently verified by this pack. No exact blade sizes, service position, connector or variant manual is supplied. Existing app content is not qualified approval of new rules. |
| OEM | Exact owner/service manuals and actual vehicle identity | Required for numerical/procedural/model-specific claims | **MISSING.** Neither the representative 2019 Japanese Raize configuration nor the 1967 US Impala catalog proves the identity, modifications or fitment of an owner's vehicle. |

## Proposed boundary table (#47)

Every row is **draft / not approved**. Wording is proposed review input only.
An absent red flag never implies that driving, inspection or a repair is safe.

| Rule | Proposed message / disposition | Source | Uncertainty / applicability | Stop boundary and reviewer question |
| --- | --- | --- | --- | --- |
| B01 | Reported loss of braking or steering is outside an owner-check flow; hand off to appropriate assistance. | HC Other problems; AA Brake | No reliable remote diagnosis; actual symptom and environment unknown | No test drive, fluid manipulation or restart branch. Reviewer must approve exact emergency/assistance wording for intended jurisdictions. |
| B02 | Reported smoke, fire, fuel leak or burning smell is outside this workflow. | HC Other problems; Overheated engines or fire; fuel leaks | Fuel type, hazard severity and local emergency routes unknown | End owner workflow; no bonnet opening, leak search or firefighting instruction. Reviewer defines locally applicable escalation language. |
| B03 | Reported overheating is outside this workflow. | HC Overheated engines or fire | Temperature indication and cooling design unknown | No hot-coolant access, cap removal or refill procedure. Reviewer resolves message and assistance route without importing the source's broader procedure. |
| B04 | Warning combinations or unfamiliar serious warnings need the exact manual and qualified assessment. | HC Warning displays; AA named warning sections | AA conditional "safe to drive" text is not equivalent to HC's broader caution; colour alone cannot classify every vehicle | No universal safe-to-drive decision, fixed waiting period or repeated-start advice. Contradiction stays unresolved pending qualified review. |
| B05 | Wiper access/fitment or parked verification that cannot be established remains uncertain. | W pending recheck; HC visibility | Exact service position, attachment and washer operation unknown | No new executable gate. Review existing wording separately against exact manuals and public evidence. |
| B06 | No-start descriptions are observations, not proof of battery failure. | AD; HC Warning displays; AA Battery | Population prevalence does not establish an individual cause | No repeated start attempts, jump-start or replacement instruction. See N table. |
| B07 | Unknown tyre specification or visibly concerning damage must not produce guessed pressure or a drive verdict. | HC Tyres; Tyre pressures | Actual fitment, load, units and thermal condition unknown | Observation/handoff only; no inflation, deflation, jacking or repair procedure. See T table. |

## No-start observation draft (#44)

Scope: describing a previously observed event in a conventional 12V starting
context. No request to reproduce the event, operate controls, open electrical
equipment or identify a failed part. Conventional 12V context itself may be
unknown. EV/high-voltage and hybrid procedures are excluded.

Proposed record: `observedAt` (or unknown), owner's verbatim description,
`startState` = no-crank / slow-crank / crank-no-start / unknown,
`warningContext` = before-start / running / unknown, warning description
(or unknown), conditions already observed, vehicle identity/applicability
evidence (or unknown), suspected cause separately labelled unconfirmed, and
assistance outcome. Existing #40/#41 observation/provenance contracts are the
future integration seam; this draft adds no second persisted record store.

| Case | Proposed reviewable text / distinction | Evidence | Proposed next state | Status / open question |
| --- | --- | --- | --- | --- |
| N01 no-crank | Owner reports no engine rotation was heard during an earlier attempt; this is a description, not a failed-battery diagnosis. | AD supports prevalence only; exact terminology needs qualified/manual validation | Observation summary -> service handoff | Draft; confirm accessible terminology and handling of silent powertrains. |
| N02 slow-crank | Owner reports unusually slow rotation compared with prior experience; subjective comparison is not a measurement or diagnosis. | No exact variant diagnostic evidence supplied | Observation summary -> service handoff | Draft; source/qualification gap stays explicit. |
| N03 crank-no-start | Owner reports rotation but no sustained running during the earlier event; do not infer fuel, ignition or battery cause. | Exact variant diagnostic evidence missing | Observation summary -> service handoff | Draft; qualified reviewer must validate distinction. |
| N04 unknown | Owner cannot reliably distinguish the event; retain their words and unknown state. | #40 unknown-preserving contract | Unknown observation -> service handoff | Draft; no forced diagnosis or repeat attempt. |
| N05 warning context | Record whether the warning was observed before start, while running or at an unknown time. These states are not interchangeable. | HC Warning displays; AA introductory and Battery sections | Observation summary -> qualified assessment | Draft; exact symbol/manual evidence missing. |
| N06 hazard or unsafe/uncertain observation conditions | Do not collect additional active observations. | B01-B04 | Assistance boundary, terminal | Draft; exact locally appropriate escalation wording requires #51. |

The ADAC statistic is **45.4% of its 2025 roadside-assistance cases involving
battery problems**, not 45.4% of all vehicles or repair visits. No Raize/Impala
failure-rate claim follows. Missing manual/variant evidence blocks numerical,
procedural and model-specific advice. Jump-start, electrical dismantling,
battery replacement and high-voltage work remain excluded.

## Tyre observation draft (#45)

Text-only, observation/handoff scope; no 3D dependency. Do not ask an owner to
approach an unsafe location or manipulate a tyre to obtain missing evidence.

| Proposed field | Allowed representation / reason |
| --- | --- |
| occurrence | Stable wheel occurrence when known; otherwise owner's description / unknown. Do not merge front/rear/left/right. |
| observed pressure | Finite positive numeric value with explicitly supplied unit (`kPa`, `bar`, `psi`), or unknown. Never assume a unit or substitute zero. This is a proposed contract, not a validated gauge reading. |
| thermal condition | cold / hot / unknown, as reported; do not convert hot readings into invented cold targets. |
| applicable specification | Exact manufacturer source locator/revision, load/fitment applicability and unit, or unknown; preserve separate from observed pressure. |
| visible concern / environment | Owner's existing description, visibility/uncertainty and whether the location was unsafe, or unknown; no roadside inspection instruction. |
| outcome | observation-only / unknown-specification / assistance-boundary; no numeric target, legal compliance or safe-to-drive result. |

| Case | Proposed reviewable disposition | Evidence / uncertainty | Next state / review status |
| --- | --- | --- | --- |
| T01 missing or mismatched manufacturer specification | Preserve observation; no recommended pressure. Sidewall maximum and model name are not an applicable target. | HC requires manufacturer's specification for load; actual handbook/placard/fitment missing | Unknown specification -> handoff; draft / not approved |
| T02 pressure without unit or thermal condition | Record missing context explicitly; do not compare to a target. | HC cold/hot distinction; unit-preservation contract proposed for review | Incomplete observation -> handoff; draft / not approved |
| T03 known source and already observed reading | Retain value, unit, cold/hot condition and exact source independently. No automatic arithmetic, adjustment or clearance to drive. | HC; actual source applicability still needs verification | Observation-only -> handoff; draft / not approved |
| T04 reported dangerous damage, flat tyre or unsafe location | End owner-check path; assistance assessment rather than repair. | HC defects/flat tyre caution; no model-specific inspection standard supplied | Assistance boundary, terminal; draft / not approved |
| T05 contradictory fitment, units or source | Retain conflict; never choose the more convenient value. | OEM evidence missing; uncertainty policy | Unresolved evidence -> handoff; draft / not approved |

No numerical pressures, tread-depth/legal thresholds, wheel removal, jacking,
torque, puncture repair or inflation/deflation instructions are supplied. ADAC's
assistance population cannot become a local tyre failure-rate claim.

## Qualified handoff and release gates

Review target is this exact file at its committed revision, including B01-B07,
N01-N06 and T01-T05. The GitHub handoff comment on #51 identifies the immutable
commit URL. Record each later revision separately; approval cannot silently
carry over to changed text.

No reviewer is appointed and no qualification is asserted. #51 must record
reviewer/qualification, exact reviewed text revision, jurisdiction/vehicle
applicability, accepted/rejected/revise disposition per row, unresolved limits,
and date. Source/rights review is distinct from technical/safety review.

Unresolved evidence: exact vehicle/manual/fitment; locally applicable hazard
and assistance wording; N01-N03 terminology evidence; AAA source recheck; warning
source conflicts; and any owner-visible numerical/procedural content. These
remain blocked, not completed by AI drafting or product approval.

Only preparation can be completed under #44/#45/#47. #51 qualified review and
#39 separately approved executable implementation remain open. Reopen the
pack for source withdrawal, changed source wording, applicability conflict or
any requested expansion. Rollback/withdrawal is removal of this draft reference;
there is no executable app behavior to roll back.
