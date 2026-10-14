namespace NorthernLink.Compliance.Domain.RegulatoryBasis;

/// <summary>
/// The stable keys of the regulatory-basis catalogue, as constants so an error definition or a
/// record-type entry can cite one without a magic string. <c>RegulatoryBasisCoverageTests</c>
/// proves every constant here exists in <c>RegulatoryBasisSeed</c> and vice versa, so a typo on
/// either side fails the build's test run rather than rendering as a dead link.
/// <para>
/// Naming: <c>HTA.&lt;section with dots&gt;</c>, <c>NSC.&lt;standard&gt;[.&lt;section&gt;]</c>,
/// <c>MR.&lt;num&gt;-&lt;year&gt;[.&lt;section&gt;]</c>, <c>SOR.&lt;year&gt;-&lt;num&gt;</c>,
/// <c>NLSP02.&lt;plan section&gt;</c>, <c>MTI.*</c> / <c>MPI.*</c> / <c>TC.*</c> for guidance.
/// </para>
/// </summary>
public static class BasisKeys
{
    // --- The Highway Traffic Act ---------------------------------------------------------------
    public const string HtaBus = "HTA.1.1.bus";
    public const string HtaRegulatedVehicle = "HTA.1.1.regulated_vehicle";
    public const string Hta134_3 = "HTA.134.3";
    public const string Hta134_4 = "HTA.134.4";
    public const string Hta155_2 = "HTA.155.2";
    public const string Hta155_5 = "HTA.155.5";
    public const string Hta155_6 = "HTA.155.6";
    public const string Hta155_7 = "HTA.155.7";
    public const string Hta185_1_1 = "HTA.185.1.1";
    public const string Hta186_3 = "HTA.186.3";
    public const string Hta186_4 = "HTA.186.4";
    public const string Hta186_6 = "HTA.186.6";
    public const string Hta186_9 = "HTA.186.9";
    public const string Hta215_1_2 = "HTA.215.1.2";
    public const string Hta265_1_1 = "HTA.265.1.1";
    public const string Hta265_1_3 = "HTA.265.1.3";
    public const string Hta312_1 = "HTA.312.1";
    public const string Hta312_2_1 = "HTA.312.2.1";
    public const string Hta312_2_7 = "HTA.312.2.7";
    public const string Hta312_2_12 = "HTA.312.2.12";
    public const string Hta312_3_2 = "HTA.312.3.2";
    public const string Hta312_3_3 = "HTA.312.3.3";
    public const string Hta312_4 = "HTA.312.4";
    public const string Hta313_1 = "HTA.313.1";
    public const string Hta314 = "HTA.314";
    public const string Hta317 = "HTA.317";
    public const string Hta317_1_1 = "HTA.317.1.1";
    public const string Hta318_1_3 = "HTA.318.1.3";
    public const string Hta318_1_4 = "HTA.318.1.4";
    public const string Hta318_2 = "HTA.318.2";
    public const string Hta318_3_1 = "HTA.318.3.1";
    public const string Hta318_3_2 = "HTA.318.3.2";
    public const string Hta318_3_2_1 = "HTA.318.3.2.1";
    public const string Hta318_4 = "HTA.318.4";
    public const string Hta318_5_1 = "HTA.318.5.1";
    public const string Hta318_5_2 = "HTA.318.5.2";
    public const string Hta318_6_1 = "HTA.318.6.1";
    public const string Hta318_6_2 = "HTA.318.6.2";
    public const string Hta318_6_3 = "HTA.318.6.3";
    public const string Hta318_6_4 = "HTA.318.6.4";
    public const string Hta318_7_1 = "HTA.318.7.1";
    public const string Hta318_7_2 = "HTA.318.7.2";
    public const string Hta318_7_3 = "HTA.318.7.3";
    public const string Hta318_8_1 = "HTA.318.8.1";
    public const string Hta318_8_2 = "HTA.318.8.2";
    public const string Hta318_9 = "HTA.318.9";
    public const string Hta318_10_2 = "HTA.318.10.2";
    public const string Hta322_1_3 = "HTA.322.1.3";
    public const string Hta322_1_11 = "HTA.322.1.11";

    // --- Manitoba regulations ------------------------------------------------------------------
    public const string Mr132_2024 = "MR.132-2024";
    public const string Mr132_2024_7_1_b = "MR.132-2024.7.1.b";
    public const string Mr132_2024_9 = "MR.132-2024.9";
    public const string Mr72_2007 = "MR.72-2007";
    public const string Mr95_2008 = "MR.95-2008";
    public const string Mr95_2008_7_3 = "MR.95-2008.7.3";
    public const string Mr95_2008_12_1 = "MR.95-2008.12.1";
    public const string Mr76_94 = "MR.76-94";
    public const string Mr76_94_3_2_a_i = "MR.76-94.3.2.a.i";
    public const string Mr28_2019 = "MR.28-2019";
    public const string Mr31_2019 = "MR.31-2019";
    public const string Mr31_2019_2_4_27 = "MR.31-2019.2.4.27";
    public const string Mr259_2014 = "MR.259-2014";
    public const string Mr259_2014_5_2 = "MR.259-2014.5.2";
    public const string Mr89_2013 = "MR.89-2013";
    public const string Mr89_2013_5_1 = "MR.89-2013.5.1";
    public const string Mr16_2019 = "MR.16-2019";
    public const string Mr16_2019_2_1 = "MR.16-2019.2.1";
    public const string Mr37_2005 = "MR.37-2005";
    public const string Mr155_2018 = "MR.155-2018";

    // --- Federal regulations -------------------------------------------------------------------
    public const string Sor2005_313 = "SOR.2005-313";
    public const string Sor2005_313_85_3 = "SOR.2005-313.85.3";
    public const string Sor2001_286 = "SOR.2001-286";

    // --- National Safety Code ------------------------------------------------------------------
    public const string Nsc4 = "NSC.4";
    public const string Nsc6 = "NSC.6";
    public const string Nsc7 = "NSC.7";
    public const string Nsc9 = "NSC.9";
    public const string Nsc9_1_OnDutyTime = "NSC.9.1.on-duty-time";
    public const string Nsc9_4 = "NSC.9.4";
    public const string Nsc9_12 = "NSC.9.12";
    public const string Nsc9_13_3 = "NSC.9.13.3";
    public const string Nsc9_14 = "NSC.9.14";
    public const string Nsc9_25_26 = "NSC.9.25-26";
    public const string Nsc9_76 = "NSC.9.76";
    public const string Nsc9_77_1 = "NSC.9.77.1";
    public const string Nsc9_77_7 = "NSC.9.77.7";
    public const string Nsc9_78_5 = "NSC.9.78.5";
    public const string Nsc9_78_6 = "NSC.9.78.6";
    public const string Nsc9_81_2 = "NSC.9.81.2";
    public const string Nsc9_85_1 = "NSC.9.85.1";
    public const string Nsc9_85_3 = "NSC.9.85.3";
    public const string Nsc9_86 = "NSC.9.86";
    public const string Nsc9_87 = "NSC.9.87";
    public const string Nsc10 = "NSC.10";
    public const string Nsc10_Part1_Div2 = "NSC.10.1.2";
    public const string Nsc10_Tiedowns = "NSC.10.4.2";
    public const string Nsc11 = "NSC.11";
    public const string Nsc11_PartA = "NSC.11.A";
    public const string Nsc11_PartA_1_c = "NSC.11.A.1.c";
    public const string Nsc12 = "NSC.12";
    public const string Nsc13 = "NSC.13";
    public const string Nsc13_4_b = "NSC.13.4.b";
    public const string Nsc13_13 = "NSC.13.13";
    public const string Nsc13_14 = "NSC.13.14";
    public const string Nsc13_15 = "NSC.13.15";
    public const string Nsc13_16 = "NSC.13.16";
    public const string Nsc14 = "NSC.14";
    public const string Nsc15 = "NSC.15";

    // --- Guidance ------------------------------------------------------------------------------
    public const string MtiSafetyMonitoring = "MTI.safety-monitoring";
    public const string MtiSfc = "MTI.sfc";
    public const string MtiCarrierProfile = "MTI.carrier-profile";
    public const string MtiGuideTransportationSafety = "MTI.guide-transportation-safety";
    public const string MtiFacilityAuditManual = "MTI.facility-audit-manual";
    public const string MtiRecordKeepingCompliancePlan = "MTI.record-keeping-compliance-plan";
    public const string MtiAccidentReporting = "MTI.accident-reporting";
    public const string MtiDriverLicenceDisclosure = "MTI.driver-licence-disclosure";
    public const string MtiGuideAppendixE = "MTI.guide-appendix-e";
    public const string MtiGuideAppendixF = "MTI.guide-appendix-f";
    public const string MtiNewCarrier = "MTI.new-carrier";
    public const string MpiDriverAbstractRequest = "MPI.driver-abstract-request";
    public const string TcEld = "TC.eld";
    public const string TcEldCertifiedList = "TC.eld-certified-list";
    public const string TcRecalls = "TC.recalls";

    // --- Safety Plan NL-SP-02 sections ----------------------------------------------------------
    public const string Plan1 = "NLSP02.1";
    public const string PlanDeclaration = "NLSP02.Declaration";
    public const string PlanDesignations = "NLSP02.Designations";
    public const string Plan2 = "NLSP02.2";
    public const string Plan3_2 = "NLSP02.3.2";
    public const string Plan3_3 = "NLSP02.3.3";
    public const string Plan3_4 = "NLSP02.3.4";
    public const string Plan3_5 = "NLSP02.3.5";
    public const string Plan4_2 = "NLSP02.4.2";
    public const string Plan4_4 = "NLSP02.4.4";
    public const string Plan4_5 = "NLSP02.4.5";
    public const string Plan4_6 = "NLSP02.4.6";
    public const string Plan5_2 = "NLSP02.5.2";
    public const string Plan5_3 = "NLSP02.5.3";
    public const string Plan5_4 = "NLSP02.5.4";
    public const string Plan5_6 = "NLSP02.5.6";
    public const string Plan6_2 = "NLSP02.6.2";
    public const string Plan6_3 = "NLSP02.6.3";
    public const string Plan7 = "NLSP02.7";
    public const string Plan8 = "NLSP02.8";
    public const string Plan9_4 = "NLSP02.9.4";
    public const string Plan10_3 = "NLSP02.10.3";
    public const string Plan11 = "NLSP02.11";
    public const string Plan12_1 = "NLSP02.12.1";
    public const string Plan13 = "NLSP02.13";
}
