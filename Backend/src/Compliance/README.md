# NorthernLink.Compliance

The compliance foundation of Safety Plan NL-SP-02: the published **rule set**, the **record
registry** with its retention lifecycle, **document numbering**, the **regulatory-basis
catalogue**, and the shared **print frame**. Schema `compliance`, endpoints `/api/compliance`,
policy `ComplianceAccess` (Owner + ComplianceOfficer).

## What this module owns — and what it does not

| Compliance owns | Owned elsewhere |
|---|---|
| The CA-MB rule set as published versions (`rule_sets`) and each tenant's adoption of one (`tenant_rule_set_applications`). The HOS section is a **display mirror** of `NorthernLink.Shared.HoursOfService.HosLimits`, built from the engine's constants — nothing here enforces hours. | **The HOS engine** (`Shared/HoursOfService`) is the law; **HOS findings, out-of-service declarations, deferrals and ELD malfunctions belong to Drivers** under the HOS plan. |
| The record registry: one `ComplianceRecord` per document the s. 11 table says must be kept — its number, subject, anchors, retain-until, holds, status — plus the destruction log. | **The documents themselves**: trip inspection reports and maintenance records (Fleet), duty status and driver files (Drivers), manifests and bills of lading (Trips), incident files (Incidents, later). Compliance only **indexes** them. |
| Document numbering: the twelve series (`HS CM DQ VM FR IR TR SP EP HR PM EQ`) per tenant per year, gap-free. | Form content and layout for every NL-xx-nn form. |
| The regulatory-basis catalogue (`regulatory_basis`): every HTA/NSC/M.R./SOR/guidance citation the plan quotes, with plain explanations and consequences. Every error this module returns carries `basis` keys into it. | Nothing — reference data, read by every module's frontend through this API. |
| The print frame (`IRecordPrintService`, QuestPDF) and the `record-cover` template. | Per-form templates land in later epics; producers keep their own PDFs. |

Cross-module flow is events only:

- **Intake** — a producer raises `ComplianceRecordProducedIntegrationEvent`
  (`Shared/IntegrationEvents/Compliance/`) into *its own* outbox; this module polls `compliance`,
  `drivers`, `fleet` and `trips` for it (`OnProducedBy`, `RecordProducerSchemas`) and registers the
  record idempotently on (`SourceModule`, `SourceRecordId`). An event carrying
  `SupersedesSourceRecordId` (appended, default null) registers as the next version of that
  producer's earlier record when it is registered and Active — the predecessor moves to
  `Superseded` and its 2-year copy clock starts.
- **Answer** — `ComplianceRecordRegisteredIntegrationEvent` carries the issued number back so the
  producer can stamp it on the document.
- **Compliance is its own producer.** Its R20 documents (SFC, insurance, registration) raise the
  produced event into `compliance.outbox_messages` through `ComplianceIntegrationEventMapper`, and
  `ComplianceRecordRegisteredIntegrationEventHandler` stamps the answer back by source-id prefix
  (`sfc:`, `insurance-policy:`, `vehicle-registration:`). Until it lands, `documentNumber` is null —
  a screen or print shows "number pending".
- **Replicas** — `vehicle_lookup`, `driver_lookup`, `user_lookup` are upserted from Fleet's,
  Drivers' and Identity's change events. Trips and Budgeting already consume those keys on the
  status-tracked path, and an outbox row has **one** `processing_status`, so a second status
  subscriber would see only the rows it won. These subscriptions (and the registered answer) use
  `OnFanOut` instead: the consumer reads the producer's rows past this module's own cursor
  (`projection_checkpoints` row `inbox:<routing-key>`) and never touches the status columns. A new
  fan-out key replays its whole history. The consumer pushes the event's `AmbientTenant` before it
  resolves the handler scope, so the scope's DbContext and its RLS session carry the right tenant.

## Record types — NL-SP-02 s. 11

| R | Record | Keeper | Carrier retention | Anchor(s) | Series |
|---|---|---|---|---|---|
| R1 | Records of duty status + supporting docs | CO | ≥ 6 months (NSC 9 s. 85(3)) | ReceivedDate + 6m | HS |
| R2 | Exempt time records (NL-HS-02) | CO | 6 months (NSC 9 s. 81(2)) | RecordDate + 6m | HS |
| R3 | Unidentified-driver profile records | CO | ≥ 6 months (NSC 9 s. 85(3)) | ReceivedDate + 6m | HS |
| R4 | ELD Malfunction Register entries (NL-HS-03) | CO | 6 months after repair (NSC 9 s. 78(6)) | RepairDate + 6m | HS |
| R5 | Notices of Non-Compliance (NL-HS-04) | CO | 2 years in driver file (NSC 9 s. 87(2)) | RecordDate + 2y | HS |
| R6 | ELD information packet | MO | current in each vehicle; superseded 6 months (NSC 9 s. 77(7)) | Superseded + 6m | HS |
| R7 | Monthly HOS audit (NL-HS-01) | CO | 2 years (NSC 9 s. 87) | MonthEnd + 2y | HS |
| R8 | Monthly Compliance Report (NL-CM-01) | CO | 2 years (HTA s. 322.1) | MonthEnd + 2y | CM |
| R9 | Trip inspection reports (NL-VM-03) | MO | month created + 6 months (NSC 13 s. 16) | MonthEnd + 6m | VM |
| R10 | SPM&I / repair records, work orders, invoices (NL-VM-02) | MO | 2 years AND 6 months after vehicle leaves control (NSC 11 Part A s. 1(c)) | later of RecordDate + 2y, VehicleLeftControl + 6m | VM |
| R11 | PMVI certificates | MO | until next inspection; copies 2 years (M.R. 76/94) | later of Superseded, RecordDate + 2y | VM |
| R12 | CVSA roadside inspection reports + repair certification | MO | 2 years (NSC 12) | RecordDate + 2y | VM |
| R13 | Manufacturer recall notices + corrective work | MO | 2 years after completion (HTA s. 318.7(2)-(3)) | CompletionDate + 2y | VM |
| R14 | Driver abstracts, fitness reviews (NL-DQ-06), disclosures (NL-DQ-04), annual file audit | CO | 2 years (HTA ss. 318.6(4), 318.8(1)) | RecordDate + 2y | DQ |
| R15 | Licence disclosure (NL-DQ-02), application, road tests (NL-DQ-05), agreements (NL-HR-03), acknowledgements (NL-SP-01-A) | CO | term of employment (HTA s. 318.8(1)) | EmploymentEnd | DQ |
| R16 | Training records (NL-TR-01, NL-TR-02, certificates) | CO | term of employment + 2 years | EmploymentEnd + 2y | TR |
| R17 | Disciplinary records (NL-DQ-07) | CO | term of employment | EmploymentEnd | DQ |
| R18 | Bills of lading / waybills (NL-FR-01), trip manifests (NL-PM-01) | CO | 2 years (HTA s. 313; M.R. 16/2019) | RecordDate + 2y | FR |
| R19 | Incident files (NL-IR-01) | CO | 2 years and until any claim is closed (HTA s. 318.1(4)) | later of RecordDate + 2y, ClaimClosed | IR |
| R20 | Insurance certificates and registration | Owner | until renewed; superseded copies 2 years (HTA ss. 312.2(7), 312.4; M.R. 132/2024) | Superseded + 2y | SP |
| R21 | Safety Plan, Safety Policy and prior versions | Owner | permanent (NSC 15) | — | SP |

CO = ComplianceOfficer, MO = MaintenanceOfficer — Safety Plan designations, not auth roles. The
table is code: `Domain/Records/ComplianceRecordCatalogue.cs`; the rule set's retention section is
built from it, never typed twice. `MonthEnd` is derived from `RecordDate` when a producer sends
only the latter. A "later of" rule stays **pending** (Active, never disposable) until every anchor
is known.

## Lifecycle

`Active` → (`RetentionWorker`, nightly 02:00 America/Winnipeg, or `POST records/disposal-review/sweep`)
→ `ReviewForDisposal` → (`POST records/dispose`, a person, batch, logged to `destruction_log`) →
`Disposed`. A hold (`POST records/{id}/holds`) keeps a record Active until released. A hold or an
anchor correction on a record already in review takes it back out (to Superseded if it carries a
`Superseded` anchor, else Active); the next sweep re-queues it only if it is still due — disposal
trusts review status, so review must never outlive the date it was decided on. For the same
reason the manual sweep refuses a future `asOf`. A new version
(`POST records/{id}/supersede`) registers a successor (`DocumentVersion + 1`) and moves the old one
to `Superseded` with its `Superseded` anchor set to today.

## Numbering

Form codes stay `NL-HS-01`; instances are `{prefix}-{series}-{formNo}-{year}-{seq:0000}`
(`NL-HS-04-2026-0031`). The prefix comes from `tenant_compliance_settings`. A series counter is an
aggregate whose version is the concurrency token; the handler issues the number and saves the
record **in the same SaveChanges**, so a lost race retries with the next number — consecutive and
gap-free (`RecordRegistrar`). Types the plan assigns no form to use their R-code as the form number
(`NL-VM-R11-2026-0003`).

## First-run

`POST /api/compliance/seed-defaults` (idempotent): default settings (prefix `NL`, Leaf Rapids,
Manitoba), publishes and applies the CA-MB rule set, opens the twelve series for the current year,
upserts the regulatory-basis catalogue. `RegisterRecord` also self-heals missing settings so a
producer's event never parks because nobody seeded.

## Carrier authority, insurance and registration (Epic 2 tasks 1–4)

| Route (under `/api/compliance`) | What it does |
|---|---|
| `GET authority` | The tenant's one `CarrierAuthority`; `exists: false` (200) is the first-run state |
| `POST authority/sfc` | Records the SFC in force. A new number or expiry moves the current one to `sfcHistory` and supersedes its R20 record |
| `POST`/`GET authority/sfc/{certificateId}/certificate` | Scan of the SFC (upload: current certificate only, else 409 `SfcNotCurrent`; download: current or superseded) |
| `POST authority/nsc-number` | The NSC number. The authority owns it; the settings print-header copy updates in the same save |
| `PUT authority/safety-rating`, `authority/intra-provincial`, `authority/routes` | Rating + as-of date; the Owner-signed intra-provincial declaration (statement kept verbatim); routes markdown |
| `GET vehicles`, `PUT vehicles/{vehicleId}/regulated` | The vehicle replica and the Compliance-only override (`true`/`false`/`null` = seating decides) |
| `GET`/`POST insurance-policies`, `GET …/{id}`, `POST …/{id}/renew` | One Current policy per vehicle; renew supersedes. A limit below the rule set's minimum is **saved and flagged** (`belowMinimum`), never refused |
| `POST insurance-policies/{id}/certificate`, `…/filing-confirmation`, `GET …/attachments/{slot}` | Certificate and the insurer's HTA s. 312.4 filing confirmation |
| `GET`/`POST vehicle-registrations`, `GET …/{id}`, `POST …/{id}/renew`, `POST …/{id}/document`, `GET …/attachments/{slot}` | Same shape; class defaults to `AR`, expiry to `effectiveOn` + the rule set's 6-month term |

- **NSC rule.** Once the authority holds an NSC number, `PUT settings` with a different one is
  409 `Compliance.Settings.NscNumberManagedByAuthority` — echo the number `GET settings` returned.
- **Regulated vehicle** = `regulatedOverride ?? seatingCapacity ≥ authority.regulatedMinSeatingCapacity`
  (11, including the driver — HTA s. 1(1) "bus"; owner question Q1). Retired, Sold and Recycled
  vehicles never count toward readiness.
- **Attachments** go to `IObjectStorage` under
  `compliance/{tenant}/{ownerKind}/{ownerId}/{slot}/{guid}{ext}` — PDF, JPEG, PNG or HEIC, at most
  15 MB, multipart field `file`, antiforgery disabled (bearer auth behind the frontends' proxy). A
  replacement is a new key; the superseded object is kept for R20. Refusals are the static
  `AttachmentErrors`. The wire never carries the storage key.
- **Rule set v2** (`CaMbRuleSets.V2026_10_12`) adds the `authority` section (warning horizons, SFC
  and insurance warning days, NET-course window 180 d, registration term 6 months, plan
  re-acknowledgement 30 d, regulated seating 11). `rule_sets.authority` is nullable: a version-1 row
  reads as `AuthorityRuleSection.Default`.
- **Persistence.** Migration `AddCarrierAuthority` adds `carrier_authorities` (unique per tenant),
  `insurance_policies` and `vehicle_registrations` (partial unique index on
  `(tenant_id, vehicle_id, status) WHERE status = 'Current'` — `status` is in the key so EF orders a
  renewal's UPDATE before its INSERT), the three replicas, and `rule_sets.authority`. Every new
  table has a query filter and the canonical tenant RLS policy.
- `tests/NorthernLink.Compliance.IntegrationTests` (Testcontainers, needs Docker) proves the
  migration, the index under renewal, RLS between tenants, the fan-out cursor leaving the
  producer's row Pending, and the full SFC register → stamp → supersede loop.

## Reference tables and RLS

`rule_sets` and `regulatory_basis` carry no `tenant_id`: RLS is enabled and forced with a
`USING (true)` read policy and a system-only write policy. Their repositories write under the
platform's system session (`Infrastructure/Persistence/SystemSession.cs`), the same mechanics the
projection and outbox workers use. Every tenant table carries the canonical tenant policy.
