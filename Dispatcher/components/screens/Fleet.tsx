"use client";

import { useCallback, useEffect, useState } from "react";
import { colors } from "@/lib/theme";
import { ApiError, listVehicles, type Vehicle } from "@/lib/api";
import { PageHeader } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import VehicleFormModal from "@/components/VehicleFormModal";
import RetirementCertificateModal from "@/components/RetirementCertificateModal";
import WorkOrderModal from "@/components/WorkOrderModal";
import ShopRegistryModal from "@/components/fleet/ShopRegistryModal";
import FleetDashboard from "@/components/screens/fleet/FleetDashboard";
import FleetMasterList from "@/components/screens/fleet/FleetMasterList";
import { FleetEmpty, FleetLoadError, FleetLoadingSkeleton } from "@/components/screens/fleet/FleetStates";
import VehicleDetail from "@/components/screens/fleet/vehicle-detail/VehicleDetail";
import { tabIndex, type ModalKind, type PromptKind } from "@/components/screens/fleet/vehicle-detail/shared";

const EYEBROW = "Operations · Fleet & Maintenance · Fleet API";
const TITLE = "Fleet & Maintenance";

// Shared frame for the pre-data states so the header stays consistent.
function ScreenFrame({ children, right }: { children: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={right} />
      </div>
      {children}
    </div>
  );
}

export default function Fleet({
  fleetSelId,
  setFleetSelId,
}: {
  fleetSelId: string | null;
  setFleetSelId: (id: string | null) => void;
}) {
  const [vehicles, setVehicles] = useState<Vehicle[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState(0);

  const [modal, setModal] = useState<ModalKind>(null);
  const [certOpen, setCertOpen] = useState(false);
  const [woVersion, setWoVersion] = useState(0);

  const [prompt, setPrompt] = useState<PromptKind>(null);
  const [reasonInput, setReasonInput] = useState("");
  const [odoInput, setOdoInput] = useState("");
  const [priceInput, setPriceInput] = useState("");

  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [autoRetired, setAutoRetired] = useState(false);

  const load = useCallback(async () => {
    // No synchronous setState before the first await — keeps this safe to call
    // from a mount effect without triggering cascading renders.
    try {
      const fresh = await listVehicles();
      setVehicles(fresh);
      setLoadError(null);
    } catch (e) {
      setVehicles(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load fleet.");
    }
  }, []);

  useEffect(() => {
    // Fetch on mount. setState only runs inside the async callbacks (never
    // synchronously in the effect body), and a mounted guard avoids updating
    // after unmount.
    let active = true;
    listVehicles().then(
      (fresh) => {
        if (active) {
          setVehicles(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setVehicles(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load fleet.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // Dashboard when nothing is selected; a vehicle's detail when one is picked.
  const sel = fleetSelId ? vehicles?.find((v) => v.id === fleetSelId) ?? null : null;

  function resetInteraction() {
    setPrompt(null);
    setActionError(null);
    setAutoRetired(false);
  }

  function selectVehicle(id: string) {
    setFleetSelId(id);
    setTab(0);
    resetInteraction();
  }

  function openVehicleTab(id: string, tabName?: string) {
    setFleetSelId(id);
    setTab(tabName ? tabIndex(tabName) : 0);
    resetInteraction();
  }

  function backToOverview() {
    setFleetSelId(null);
    resetInteraction();
  }

  function onRegistered(newId?: string) {
    load();
    if (newId) {
      // Honour "scan docs at registration" — land on the new vehicle's docs tab.
      setFleetSelId(newId);
      setTab(tabIndex("Documents & Compliance"));
    }
  }

  async function runAction(fn: () => Promise<void>, opts?: { detectAutoRetire?: boolean }) {
    if (!sel || busy) return;
    const selId = sel.id;
    const prevStatus = sel.status;
    setBusy(true);
    setActionError(null);
    try {
      await fn();
      const fresh = await listVehicles();
      setVehicles(fresh);
      if (opts?.detectAutoRetire) {
        const updated = fresh.find((v) => v.id === selId);
        if (updated && prevStatus !== "Retired" && updated.status === "Retired") {
          setAutoRetired(true);
        }
      }
      setPrompt(null);
      setReasonInput("");
      setOdoInput("");
      setPriceInput("");
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Action failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  const headerActions = (
    <div style={{ display: "flex", gap: 9 }}>
      <ActionButton onClick={() => setModal("shops")}>SHOPS & PARTNERS</ActionButton>
      <ActionButton onClick={() => setModal("workorder")}>+ NEW WORK ORDER</ActionButton>
      <ActionButton variant="primary" onClick={() => setModal("register")}>
        + REGISTER VEHICLE
      </ActionButton>
    </div>
  );

  // -------------------------------------------------------------------------
  // Load / error / empty states
  // -------------------------------------------------------------------------

  if (loadError) {
    return (
      <ScreenFrame>
        <FleetLoadError message={loadError} onRetry={load} />
      </ScreenFrame>
    );
  }

  if (vehicles === null) {
    return (
      <ScreenFrame>
        <FleetLoadingSkeleton />
      </ScreenFrame>
    );
  }

  if (vehicles.length === 0) {
    return (
      <ScreenFrame right={headerActions}>
        <FleetEmpty onRegister={() => setModal("register")} />
        {modal === "register" && (
          <VehicleFormModal vehicle={null} onClose={() => setModal(null)} onSaved={onRegistered} />
        )}
        {modal === "shops" && <ShopRegistryModal onClose={() => setModal(null)} />}
      </ScreenFrame>
    );
  }

  // -------------------------------------------------------------------------
  // Loaded — master list + (dashboard | vehicle detail)
  // -------------------------------------------------------------------------

  const vehicleOptions = vehicles.map((v) => ({
    id: v.id,
    unit: v.unitNumber,
    label: `${v.unitNumber} · ${v.make} ${v.model}`,
  }));

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={headerActions} />
      </div>
      <div
        style={{
          flex: 1,
          minHeight: 0,
          display: "grid",
          gridTemplateColumns: "40% 1fr",
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        {/* master list (real API) */}
        <FleetMasterList
          vehicles={vehicles}
          selId={sel?.id ?? null}
          onSelect={selectVehicle}
          onOverview={backToOverview}
        />

        {/* detail pane */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {sel === null ? (
            <FleetDashboard vehicles={vehicles} woVersion={woVersion} onOpenVehicle={openVehicleTab} />
          ) : (
            <VehicleDetail
              f={sel}
              tab={tab}
              setTab={setTab}
              vehicleOptions={vehicleOptions}
              prompt={prompt}
              setPrompt={setPrompt}
              reasonInput={reasonInput}
              setReasonInput={setReasonInput}
              odoInput={odoInput}
              setOdoInput={setOdoInput}
              priceInput={priceInput}
              setPriceInput={setPriceInput}
              busy={busy}
              actionError={actionError}
              autoRetired={autoRetired}
              runAction={runAction}
              setModal={setModal}
              setCertOpen={setCertOpen}
            />
          )}
        </div>
      </div>

      {modal === "register" && (
        <VehicleFormModal vehicle={null} onClose={() => setModal(null)} onSaved={onRegistered} />
      )}
      {modal === "edit" && sel && (
        <VehicleFormModal vehicle={sel} onClose={() => setModal(null)} onSaved={() => load()} />
      )}
      {modal === "workorder" && (
        <WorkOrderModal
          vehicles={vehicleOptions}
          defaultVehicleId={sel?.id}
          onClose={() => setModal(null)}
          onSaved={() => setWoVersion((n) => n + 1)}
        />
      )}
      {modal === "shops" && <ShopRegistryModal onClose={() => setModal(null)} />}
      {certOpen && sel && (
        <RetirementCertificateModal vehicleId={sel.id} onClose={() => setCertOpen(false)} />
      )}
    </div>
  );
}
