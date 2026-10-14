namespace NorthernLink.Compliance.Domain.Records;

/// <summary>
/// The 21 rows of NL-SP-02 s. 11 (Records and audits) — every kind of document the carrier must
/// keep, numbered as the plan numbers them. The <em>name</em> ("R9") is what travels on the wire
/// and into jsonb, never the integer, so the enum can never be reordered into a different row.
/// Display names, forms, keepers and retention rules live in <see cref="ComplianceRecordCatalogue"/>.
/// </summary>
public enum ComplianceRecordType
{
    /// <summary>Records of duty status and supporting documents.</summary>
    R1 = 1,

    /// <summary>Exempt time records (NL-HS-02).</summary>
    R2,

    /// <summary>Unidentified-driver profile records.</summary>
    R3,

    /// <summary>ELD Malfunction Register entries (NL-HS-03).</summary>
    R4,

    /// <summary>Notices of Non-Compliance (NL-HS-04).</summary>
    R5,

    /// <summary>ELD information packet.</summary>
    R6,

    /// <summary>Monthly HOS audit (NL-HS-01).</summary>
    R7,

    /// <summary>Monthly Compliance Report (NL-CM-01).</summary>
    R8,

    /// <summary>Trip inspection reports (NL-VM-03).</summary>
    R9,

    /// <summary>SPM&amp;I / repair records, work orders, invoices (NL-VM-02).</summary>
    R10,

    /// <summary>PMVI certificates.</summary>
    R11,

    /// <summary>CVSA roadside inspection reports and repair certification.</summary>
    R12,

    /// <summary>Manufacturer recall notices and corrective work.</summary>
    R13,

    /// <summary>Driver abstracts, fitness reviews (NL-DQ-06), accident/conviction disclosures (NL-DQ-04), annual file audit.</summary>
    R14,

    /// <summary>Licence disclosure (NL-DQ-02), application, road tests (NL-DQ-05), agreements (NL-HR-03), acknowledgements (NL-SP-01-A).</summary>
    R15,

    /// <summary>Training records (NL-TR-01, NL-TR-02, certificates).</summary>
    R16,

    /// <summary>Disciplinary records (NL-DQ-07, warnings, letters).</summary>
    R17,

    /// <summary>Bills of lading / waybills (NL-FR-01), trip manifests (NL-PM-01).</summary>
    R18,

    /// <summary>Incident files (NL-IR-01).</summary>
    R19,

    /// <summary>Insurance certificates and registration.</summary>
    R20,

    /// <summary>Safety Plan, Safety Policy and prior versions.</summary>
    R21,
}

/// <summary>
/// Who is <em>designated</em> to keep a record type under NL-SP-02 s. 11. A designation inside the
/// Safety Plan, not an authorization role: the API's gate is <c>AuthorizationPolicies.ComplianceAccess</c>
/// regardless of keeper, and "MaintenanceOfficer" is nobody's login role.
/// </summary>
public enum RecordKeeper
{
    Owner,
    ComplianceOfficer,
    MaintenanceOfficer,
}

/// <summary>The dated event a retention period counts from.</summary>
public enum AnchorKind
{
    /// <summary>The date the record was made (the default anchor).</summary>
    RecordDate,

    /// <summary>The date the carrier received the record from the driver (NSC 9 s. 85(3)).</summary>
    ReceivedDate,

    /// <summary>The last day of the month the record belongs to.</summary>
    MonthEnd,

    /// <summary>The date the defective device was repaired or replaced (NSC 9 s. 78(6)).</summary>
    RepairDate,

    /// <summary>The date corrective work was completed (HTA s. 318.7(2)-(3)).</summary>
    CompletionDate,

    /// <summary>The end of the driver's employment or contract.</summary>
    EmploymentEnd,

    /// <summary>The date the vehicle left the carrier's control (NSC 11 Part A s. 1(c)).</summary>
    VehicleLeftControl,

    /// <summary>The date the last claim arising from the incident was closed.</summary>
    ClaimClosed,

    /// <summary>The date a newer version replaced this record.</summary>
    Superseded,
}

/// <summary>Lifecycle of a registered record.</summary>
public enum ComplianceRecordStatus
{
    /// <summary>Within its retention period (or the period cannot yet be computed).</summary>
    Active,

    /// <summary>Retention has passed and no hold applies — a person must confirm disposal.</summary>
    ReviewForDisposal,

    /// <summary>Destroyed; a <see cref="DestructionLogEntry"/> says when, by whom and how.</summary>
    Disposed,

    /// <summary>Replaced by a newer version; retained for its own period from the supersession date.</summary>
    Superseded,
}

/// <summary>What a record is about.</summary>
public enum RecordSubjectKind
{
    Carrier,
    Driver,
    Vehicle,
    Trip,
    Incident,
}
