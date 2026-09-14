"use client";

import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import { currentPeriod, type Period } from "@/lib/period";
import Clients from "@/components/screens/Clients";
import Riders from "@/components/screens/Riders";
import Billing from "@/components/screens/Billing";
import Reports from "@/components/screens/Reports";
import type { ReportTabId } from "@/components/screens/reports/shared";

// Commercial — clients and contracts, riders, invoicing and reports.
export default function CommercialApp({ screen, shell }: AppProps) {
  const [clientSel, setClientSel] = useRetainedState<string | null>("commercial.clientSel", null); // Clients API Guid
  const [invoiceSelId, setInvoiceSelId] = useRetainedState<string | null>("commercial.invoiceSelId", null); // Billing API Guid
  // Both reports' selections live here for the same reason as tripPeriod: a
  // dispatcher who built March's report should not lose it on a detour to
  // another screen — including which of the two reports they were on.
  const [reportTab, setReportTab] = useRetainedState<ReportTabId>("commercial.reportTab", "accruals");
  const [reportClientId, setReportClientId] = useRetainedState<string | null>("commercial.reportClientId", null); // Clients API Guid
  const [reportPeriod, setReportPeriod] = useRetainedState<Period>("commercial.reportPeriod", () => currentPeriod("month"));
  // The terminus report gets its OWN period, deliberately. Accruals is locked to
  // a month and so renders no granularity pills; sharing one Period would let a
  // quarter set here strand the accruals view stepping three months at a time
  // with no way back — and print "Q3 2026" onto a monthly statement.
  const [terminusStopId, setTerminusStopId] = useRetainedState<string | null>("commercial.terminusStopId", null); // Stops API Guid
  const [terminusPeriod, setTerminusPeriod] = useRetainedState<Period>("commercial.terminusPeriod", () => currentPeriod("month"));

  return (
    <>
      {screen === "clients" && <Clients clientSel={clientSel} setClientSel={setClientSel} onCreateTrip={shell.createTrip} />}
      {screen === "riders" && <Riders />}
      {screen === "billing" && <Billing invoiceSelId={invoiceSelId} setInvoiceSelId={setInvoiceSelId} />}
      {screen === "reports" && (
        <Reports
          tab={reportTab}
          setTab={setReportTab}
          clientId={reportClientId}
          setClientId={setReportClientId}
          period={reportPeriod}
          setPeriod={setReportPeriod}
          terminusStopId={terminusStopId}
          setTerminusStopId={setTerminusStopId}
          terminusPeriod={terminusPeriod}
          setTerminusPeriod={setTerminusPeriod}
        />
      )}
    </>
  );
}
