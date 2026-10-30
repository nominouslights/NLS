using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.RegulatoryBasis;

namespace NorthernLink.Compliance.Domain.Records;

/// <summary>
/// One row of the NL-SP-02 s. 11 table, as code. <see cref="Rule"/> is what the retention
/// calculator runs; <see cref="RegulatoryMinimum"/> and <see cref="CarrierRetention"/> are the
/// row's text, printed on cover sheets and binders. <see cref="Keeper"/> is a Safety Plan
/// designation, not an auth role.
/// </summary>
public sealed record RecordTypeDefinition(
    ComplianceRecordType Type,
    string DisplayName,
    IReadOnlyList<string> FormCodes,
    string SectionElevenRow,
    RecordKeeper Keeper,
    string RegulatoryMinimum,
    string Citation,
    string CarrierRetention,
    RetentionRule Rule,
    DocumentSeriesCode Series,
    string PrintTemplateKey,
    IReadOnlyList<string> RegulatoryBasisKeys)
{
    /// <summary>"R9" — the stable code, also the enum name.</summary>
    public string Code => Type.ToString();

    public IReadOnlyList<AnchorKind> AnchorKinds => Rule.Anchors;

    /// <summary>
    /// The form number stamped into instance numbers: the primary form's two digits ("03" for
    /// NL-VM-03). Types the plan assigns no form to (PMVI certificates, CVSA reports, recall
    /// notices, insurance, the plan itself) use their R-code, so <c>NL-VM-R11-2026-0003</c>
    /// still says what it is.
    /// </summary>
    public string FormNumber =>
        FormCodes.Count > 0 ? DocumentNumberFormat.FormNumberOf(FormCodes[0]) ?? Code : Code;
}

/// <summary>
/// The s. 11 table of NL-SP-02, one entry per <see cref="ComplianceRecordType"/>. This is the
/// single source for retention: the rule set's retention section is built <em>from</em> this
/// table, never typed a second time.
/// </summary>
public static class ComplianceRecordCatalogue
{
    public const string CoverTemplate = "record-cover";

    private static readonly IReadOnlyDictionary<ComplianceRecordType, RecordTypeDefinition> ByType = Build();

    public static IReadOnlyList<RecordTypeDefinition> All { get; } =
        ByType.Values.OrderBy(d => d.Type).ToList();

    public static RecordTypeDefinition For(ComplianceRecordType type) => ByType[type];

    public static bool TryParse(string? name, out ComplianceRecordType type) =>
        Enum.TryParse(name, ignoreCase: false, out type) && ByType.ContainsKey(type);

    private static Dictionary<ComplianceRecordType, RecordTypeDefinition> Build()
    {
        var co = RecordKeeper.ComplianceOfficer;
        var mo = RecordKeeper.MaintenanceOfficer;
        var owner = RecordKeeper.Owner;

        RecordTypeDefinition[] rows =
        [
            new(ComplianceRecordType.R1, "Records of duty status and supporting documents", [],
                "Records of duty status + supporting docs — carrier ≥ 6 months", co,
                "6 months after receipt", "NSC 9 s. 85(3)", "At least 6 months",
                RetentionRule.After(AnchorKind.ReceivedDate, 6), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_85_3, BasisKeys.Sor2005_313_85_3, BasisKeys.Hta318_8_1, BasisKeys.Plan4_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R2, "Exempt time records", ["NL-HS-02"],
                "Exempt time records (NL-HS-02) — 6 months", co,
                "6 months", "NSC 9 s. 81(2)", "6 months",
                RetentionRule.After(AnchorKind.RecordDate, 6), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_81_2, BasisKeys.Plan4_5, BasisKeys.Plan11]),

            new(ComplianceRecordType.R3, "Unidentified-driver profile records", [],
                "Unidentified-driver profile records — ≥ 6 months", co,
                "6 months after receipt", "NSC 9 s. 85(3)", "At least 6 months",
                RetentionRule.After(AnchorKind.ReceivedDate, 6), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_85_3, BasisKeys.Plan4_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R4, "ELD Malfunction Register entries", ["NL-HS-03"],
                "ELD Malfunction Register entries (NL-HS-03) — 6 months after repair/replacement", co,
                "6 months after repair or replacement", "NSC 9 s. 78(6)", "6 months after repair",
                RetentionRule.After(AnchorKind.RepairDate, 6), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_78_6, BasisKeys.Nsc9_78_5, BasisKeys.Plan4_5, BasisKeys.Plan11]),

            new(ComplianceRecordType.R5, "Notices of Non-Compliance", ["NL-HS-04"],
                "Notices of Non-Compliance (NL-HS-04) — 2 years in driver file", co,
                "Record of non-compliance and remedial action", "NSC 9 s. 87(2)", "2 years in the driver file",
                RetentionRule.After(AnchorKind.RecordDate, 24), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_87, BasisKeys.Plan4_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R6, "ELD information packet", [],
                "ELD information packet — current version in each vehicle, superseded kept 6 months", mo,
                "Current version in each vehicle", "NSC 9 s. 77(7)", "Superseded versions 6 months",
                RetentionRule.After(AnchorKind.Superseded, 6), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_77_7, BasisKeys.Nsc9_77_1, BasisKeys.TcEld, BasisKeys.Plan4_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R7, "Monthly HOS audit", ["NL-HS-01"],
                "Monthly HOS audit (NL-HS-01) — 2 years", co,
                "Carrier must monitor driver compliance", "NSC 9 s. 87", "2 years",
                RetentionRule.After(AnchorKind.MonthEnd, 24), DocumentSeriesCode.HS, CoverTemplate,
                [BasisKeys.Nsc9_87, BasisKeys.Plan4_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R8, "Monthly Compliance Report", ["NL-CM-01"],
                "Monthly Compliance Report (NL-CM-01) — 2 years", co,
                "Carrier profile review by the director", "HTA s. 322.1", "2 years",
                RetentionRule.After(AnchorKind.MonthEnd, 24), DocumentSeriesCode.CM, CoverTemplate,
                [BasisKeys.Hta322_1_3, BasisKeys.Nsc7, BasisKeys.MtiCarrierProfile, BasisKeys.Plan13, BasisKeys.Plan11]),

            new(ComplianceRecordType.R9, "Trip inspection reports", ["NL-VM-03"],
                "Trip inspection reports (NL-VM-03) — month created + 6 months", mo,
                "6 months", "NSC 13 s. 16", "Month created + 6 months",
                RetentionRule.After(AnchorKind.MonthEnd, 6), DocumentSeriesCode.VM, CoverTemplate,
                [BasisKeys.Nsc13_16, BasisKeys.Nsc13_15, BasisKeys.Hta318_2, BasisKeys.Mr95_2008_7_3, BasisKeys.Plan5_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R10, "SPM&I and repair records, work orders, invoices", ["NL-VM-02"],
                "SPM&I/repair records, work orders, invoices (NL-VM-02) — 2 years AND 6 months after the vehicle leaves control", mo,
                "2 years, and 6 months after the vehicle leaves the carrier's control", "NSC 11 Part A s. 1(c)", "Later of 2 years and 6 months after disposal",
                RetentionRule.LaterOf(new RetentionTerm(AnchorKind.RecordDate, 24), new RetentionTerm(AnchorKind.VehicleLeftControl, 6)),
                DocumentSeriesCode.VM, CoverTemplate,
                [BasisKeys.Nsc11_PartA_1_c, BasisKeys.Nsc11_PartA, BasisKeys.Hta318_7_3, BasisKeys.Plan5_2, BasisKeys.Plan11]),

            new(ComplianceRecordType.R11, "PMVI certificates", [],
                "PMVI certificates — until next inspection, copies 2 years", mo,
                "Until the next periodic inspection", "M.R. 76/94", "Later of supersession and 2 years",
                RetentionRule.LaterOf(new RetentionTerm(AnchorKind.Superseded, 0), new RetentionTerm(AnchorKind.RecordDate, 24)),
                DocumentSeriesCode.VM, CoverTemplate,
                [BasisKeys.Mr76_94, BasisKeys.Mr76_94_3_2_a_i, BasisKeys.Plan5_3, BasisKeys.Plan11]),

            new(ComplianceRecordType.R12, "CVSA roadside inspection reports and repair certification", [],
                "CVSA roadside inspection reports + repair certification — 2 years", mo,
                "2 years", "NSC 12", "2 years",
                RetentionRule.After(AnchorKind.RecordDate, 24), DocumentSeriesCode.VM, CoverTemplate,
                [BasisKeys.Nsc12, BasisKeys.Hta312_3_2, BasisKeys.Plan4_6, BasisKeys.Plan11]),

            new(ComplianceRecordType.R13, "Manufacturer recall notices and corrective work", [],
                "Manufacturer recall notices + corrective work — 2 years after completion", mo,
                "Record of each remedy action", "HTA s. 318.7(2)-(3)", "2 years after completion",
                RetentionRule.After(AnchorKind.CompletionDate, 24), DocumentSeriesCode.VM, CoverTemplate,
                [BasisKeys.Hta318_7_2, BasisKeys.Hta318_7_3, BasisKeys.TcRecalls, BasisKeys.Plan5_2, BasisKeys.Plan11]),

            new(ComplianceRecordType.R14, "Driver abstracts, fitness reviews, disclosures and annual file audit", ["NL-DQ-06", "NL-DQ-04"],
                "Driver abstracts, fitness reviews (NL-DQ-06), accident/conviction disclosures (NL-DQ-04), annual file audit — 2 years", co,
                "Record of each review; disclosures kept", "HTA ss. 318.6(4), 318.8(1)", "2 years",
                RetentionRule.After(AnchorKind.RecordDate, 24), DocumentSeriesCode.DQ, CoverTemplate,
                [BasisKeys.Hta318_6_4, BasisKeys.Hta318_6_1, BasisKeys.Hta318_6_2, BasisKeys.Hta318_6_3, BasisKeys.Hta318_1_3, BasisKeys.Hta318_8_1, BasisKeys.MpiDriverAbstractRequest, BasisKeys.Plan3_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R15, "Licence disclosure, application, road tests, agreements, acknowledgements", ["NL-DQ-02", "NL-DQ-05", "NL-HR-03", "NL-SP-01-A"],
                "Licence disclosure (NL-DQ-02), application, road tests (NL-DQ-05), agreements (NL-HR-03), acknowledgements (NL-SP-01-A) — term of employment/contract", co,
                "Kept as a motor-carrier record", "HTA s. 318.8(1)", "Term of employment or contract",
                RetentionRule.After(AnchorKind.EmploymentEnd, 0), DocumentSeriesCode.DQ, CoverTemplate,
                [BasisKeys.Hta318_8_1, BasisKeys.MtiDriverLicenceDisclosure, BasisKeys.Plan3_2, BasisKeys.Plan11]),

            new(ComplianceRecordType.R16, "Training records", ["NL-TR-01", "NL-TR-02"],
                "Training records (NL-TR-01, NL-TR-02, certificates) — term of employment + 2 years", co,
                "Kept as a motor-carrier record", "HTA s. 318.8(1)", "Term of employment plus 2 years",
                RetentionRule.After(AnchorKind.EmploymentEnd, 24), DocumentSeriesCode.TR, CoverTemplate,
                [BasisKeys.Hta318_8_1, BasisKeys.Plan9_4, BasisKeys.Plan11]),

            new(ComplianceRecordType.R17, "Disciplinary records", ["NL-DQ-07"],
                "Disciplinary records (NL-DQ-07, warnings, letters) — ongoing/term of employment", co,
                "Kept as a motor-carrier record", "HTA s. 318.8(1)", "Term of employment",
                RetentionRule.After(AnchorKind.EmploymentEnd, 0), DocumentSeriesCode.DQ, CoverTemplate,
                [BasisKeys.Hta318_8_1, BasisKeys.Plan3_5, BasisKeys.Plan11]),

            new(ComplianceRecordType.R18, "Bills of lading, waybills and trip manifests", ["NL-FR-01", "NL-PM-01"],
                "Bills of lading/waybills (NL-FR-01), trip manifests (NL-PM-01) — 2 years", co,
                "Bill of lading required for goods carried for compensation", "HTA s. 313; M.R. 16/2019", "2 years",
                RetentionRule.After(AnchorKind.RecordDate, 24), DocumentSeriesCode.FR, CoverTemplate,
                [BasisKeys.Hta313_1, BasisKeys.Mr16_2019, BasisKeys.Mr16_2019_2_1, BasisKeys.Plan6_3, BasisKeys.Plan11]),

            new(ComplianceRecordType.R19, "Incident files", ["NL-IR-01"],
                "Incident files (NL-IR-01) — 2 years and until any claim is closed", co,
                "Accident particulars to the director within 30 days", "HTA s. 318.1(4)", "Later of 2 years and claim closed",
                RetentionRule.LaterOf(new RetentionTerm(AnchorKind.RecordDate, 24), new RetentionTerm(AnchorKind.ClaimClosed, 0)),
                DocumentSeriesCode.IR, CoverTemplate,
                [BasisKeys.Hta318_1_4, BasisKeys.Hta155_6, BasisKeys.MtiAccidentReporting, BasisKeys.Plan10_3, BasisKeys.Plan11]),

            new(ComplianceRecordType.R20, "Insurance certificates and registration", [],
                "Insurance certificates and registration — until renewed, superseded copies 2 years", owner,
                "Insurance coverage required for the SFC; 15 days' notice of change", "HTA ss. 312.2(7), 312.4; M.R. 132/2024", "Superseded copies 2 years",
                RetentionRule.After(AnchorKind.Superseded, 24), DocumentSeriesCode.SP, CoverTemplate,
                [BasisKeys.Hta312_2_7, BasisKeys.Hta312_4, BasisKeys.Mr132_2024_7_1_b, BasisKeys.Plan12_1, BasisKeys.Plan11]),

            new(ComplianceRecordType.R21, "Safety Plan, Safety Policy and prior versions", ["NL-SP-02", "NL-SP-01"],
                "Safety Plan, Safety Policy and prior versions — permanent", owner,
                "Available at the main place of business for facility audit", "NSC 15", "Permanent",
                RetentionRule.Permanent, DocumentSeriesCode.SP, CoverTemplate,
                [BasisKeys.Nsc15, BasisKeys.MtiFacilityAuditManual, BasisKeys.Plan11]),
        ];

        return rows.ToDictionary(r => r.Type);
    }
}
