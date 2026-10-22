# Privacy-minimal product baseline (OPS02)

Research-plan artifact only, 2026-10-22. No participant is recruited, observed
or recorded; no telemetry SDK, tracking, survey endpoint or personal-data store
is added. PIL01 still requires a separately authorized protocol, participants,
release, consent and accountable research/content owner.

## Questions, not claimed findings

Hypotheses: owners can find applicable content, distinguish representative
mechanics from actual vehicle fitment, describe uncertainty without diagnosis,
and recognize when a professional handoff is more useful than another check.
Demand, usefulness, completion, safety comprehension, review capacity and cost
are all **unmeasured**. Catalog size or attractive 3D is not proof of value.

## Neutral desk-only scenarios

Use synthetic catalog/observation examples only. No real vehicle inspection,
control operation, driving, pressure reading or hazardous reproduction.

| Task | Neutral prompt | Observe without coaching the answer |
| --- | --- | --- |
| Applicable content | Find the existing Raize engine subject and say what the displayed vehicle identity does and does not establish about a real car. Repeat with a missing Impala child lesson. | Canonical subject found; configuration vs actual vehicle explained; missing content recognized rather than guessed. |
| Limitation comprehension | Explain the diagram's schematic disclaimer and distinguish a generic four-stroke explanation from model-specific service instructions. | Participant's own explanation, not only reading the disclaimer aloud; note uncertainty and incorrect fitment assumptions. |
| Observation | Using a supplied fictional symptom card, record what is known and unknown in the tab-only log, correct one entry and prepare a handoff omitting an unneeded section. | Observations remain distinct from diagnosis, unknowns stay explicit, correction trail is understood, no invented values; explain reload loss. |
| Handoff / stop | Given a fictional worsening or untestable outcome, find an appropriate handoff and explain why the guide is not permission to drive or a diagnosis. | Useful stop/service outcome and reasoning; no reward for proceeding farther or completing an unsafe path. |
| Missing content demand | Describe the missing configuration/capability in a synthetic request example; distinguish a request from a promise of publication or paid priority. | Need understood without pretending the service is deployed. Record scenario result only, not real owner identifiers. |

## Observation rubric and measurement definitions

Use task-level codes: independent / needed neutral clarification / needed
instruction / not completed; comprehension: accurate / incomplete / incorrect /
not assessed; and handoff: appropriate / unsafe inference / uncertain /
not assessed. A completed task with an unsafe inference is not success.
Record blocking step and whether an availability/error message was understood,
not an unrestricted transcript. Separate observer interpretation from the
participant's observable action.

| Measure | Definition / denominator | Current state |
| --- | --- | --- |
| Task completion and comprehension | Counts by rubric per attempted task; report sample size and missing assessments, not a lone percentage | No baseline |
| Useful stop/handoff | Appropriate stop or professional-handoff choices with correctly explained limits / eligible scenarios attempted | No baseline |
| Demand / fulfillment | Distinct eligible requested configurations, classified as complete or ambiguous, then content reviewed/published matching that request; requests are not duplicates merely because unknown fields overlap | No request service or demand data |
| Correction latency | Time from an accepted actionable content report to a reviewed correction/withdrawal, with waiting-for-evidence and review time separate | No operational baseline or SLA |
| Content preparation effort | Actual contributor/reviewer active effort per bounded accepted package, rework and source/applicability review recorded separately | Unknown; no savings claim |
| Reliability | Task-blocking failures per attempted session/build/path with environment and affected configuration; distinguish browser/platform issues and unavailable coverage | Automated regressions are engineering evidence, not user reliability statistics |
| Operating/support cost | Actual invoiced hosting/provider/storage cost, review/support effort and cost attributable to accepted scope | Unmeasured; no fabricated budget |

## Consent, minimization and handling gates

Before research, the accountable owner must approve understandable consent,
purpose, voluntary participation/withdrawal, what task-level data is retained,
who can access it, a finite retention period and a deletion mechanism. None is
assumed from using the workshop. No covert recordings, fingerprinting, geolocation
or automatic screen capture. Prefer manual synthetic-task aggregates.

Exclude VIN, plate, location, contact/account details, private observations,
free-text complaints and uploaded vehicle records from general analytics. Do
not ask participants to put real private content into ChatGPT; that optional
external feature is not a research data sink. Any separately consented contact
log must stay separate from task observations and is outside this plan.
If retention/owner/deletion choices are missing, collect nothing.

## Baseline-based decision rubric

First report sample composition/limitations and the observed task/error/
comprehension baseline; do not invent numeric thresholds, dates or guarantees.
The PO/research/content owners then set a justified next bounded experiment.

- **Continue** a bounded learning slice when observations support useful
  comprehension/handoff and review/operating capacity can sustain it.
- **Narrow** when errors concentrate on one component, uncertainty message,
  interaction or source gap; change that cause and repeat comparable scenarios.
- **Defer** executable guidance or shared requests when qualification, governance,
  provider/privacy approval or capacity is absent, regardless of completion rate.

R1 learning exits use comprehension and applicable-source evidence; R2 requests
need actual durability/privacy/auth/recovery evidence and demand; later whole-car
coverage needs reviewed content and demonstrated owner value. Record hypotheses,
measured results and unknowns in separate columns. No current roadmap checkpoint
is marked passed by this protocol.
