"use client";

import type { Period } from "@/lib/period";
import ClientAccruals from "@/components/screens/reports/ClientAccruals";
import TerminusSummary from "@/components/screens/reports/TerminusSummary";
import { ReportTabs, type ReportTabId } from "@/components/screens/reports/shared";

// Reports — the shell around the console's reports. It owns nothing but the
// switch: each report keeps its own PageHeader (the action buttons are driven
// by report-local state) and receives the tab bar to render inside its own
// header block.
//
// The two reports do NOT share a period. Client Accruals is locked to a month
// and so renders no granularity pills; handing it a period left on "quarter"
// would strand it stepping three months at a time with no way back, and would
// print "Q3 2026" onto a sheet that calls itself a monthly statement.

export default function Reports({
  tab,
  setTab,
  clientId,
  setClientId,
  period,
  setPeriod,
  terminusStopId,
  setTerminusStopId,
  terminusPeriod,
  setTerminusPeriod,
}: {
  tab: ReportTabId;
  setTab: (t: ReportTabId) => void;
  clientId: string | null;
  setClientId: (id: string | null) => void;
  period: Period;
  setPeriod: (p: Period) => void;
  terminusStopId: string | null;
  setTerminusStopId: (id: string | null) => void;
  terminusPeriod: Period;
  setTerminusPeriod: (p: Period) => void;
}) {
  const tabs = <ReportTabs tab={tab} setTab={setTab} />;

  return tab === "accruals" ? (
    <ClientAccruals
      clientId={clientId}
      setClientId={setClientId}
      period={period}
      setPeriod={setPeriod}
      tabs={tabs}
    />
  ) : (
    <TerminusSummary
      stopId={terminusStopId}
      setStopId={setTerminusStopId}
      period={terminusPeriod}
      setPeriod={setTerminusPeriod}
      tabs={tabs}
    />
  );
}
