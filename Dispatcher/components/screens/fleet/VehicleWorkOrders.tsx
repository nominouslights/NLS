"use client";

import { useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError, formatUtcDate, type Vehicle } from "@/lib/api";
import {
  changeWorkOrderStatus,
  listShops,
  listVehicleWorkOrders,
  type ShopWire,
  type WorkOrderStatusWire,
  type WorkOrderWire,
} from "@/lib/api/maintenance";
import {
  OPEN_WIRE_STATUSES,
  toPrintableShop,
  toPrintableWorkOrder,
  WO_SOURCE_LABEL,
  WO_STATUS_LABEL,
  workOrderKindWire,
} from "@/lib/workOrderDisplay";
import { COMPANY } from "@/lib/company";
import { openPrintDocument } from "@/lib/documents/printDocument";
import { workOrderHtml } from "@/lib/documents/workOrderPdf";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import WorkOrderModal from "@/components/WorkOrderModal";
import ServiceRecordModal from "@/components/ServiceRecordModal";

// Per-vehicle work orders (live Fleet API). Create manually, advance status,
// close by logging the service that resolved it, and print the NL-WO-01 form
// to take to the shop.

export default function VehicleWorkOrders({
  vehicle,
  vehicles,
}: {
  vehicle: Vehicle;
  vehicles: VehicleOption[];
}) {
  const [newOpen, setNewOpen] = useState(false);
  const [closing, setClosing] = useState<WorkOrderWire | null>(null);
  const [reload, setReload] = useState(0);
  const [rowBusy, setRowBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [shops, setShops] = useState<ShopWire[]>([]);
  // Rows are tagged with the vehicle they were fetched for, so switching
  // vehicles never shows another unit's work orders while a fetch is in flight.
  const [fetched, setFetched] = useState<{ vehicleId: string; rows: WorkOrderWire[] } | null>(null);

  const unit = vehicle.unitNumber;
  const vehicleId = vehicle.id;
  const rows = fetched?.vehicleId === vehicleId ? fetched.rows : null;

  useEffect(() => {
    let active = true;
    listVehicleWorkOrders(vehicleId).then(
      (fresh) => {
        if (active) {
          setFetched({ vehicleId, rows: fresh });
          setLoadError(null);
        }
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load work orders.");
      },
    );
    // Shops feed the printed NL-WO-01 §2. Fail-soft: printing just leaves §2 blank.
    listShops().then(
      (fresh) => {
        if (active) setShops(fresh);
      },
      (e) => {
        console.error("Shops unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, [vehicleId, reload]);

  function printWorkOrder(w: WorkOrderWire) {
    const shop = shops.find((s) => s.id === w.shopId);
    openPrintDocument(
      `Work Order ${w.number}`,
      workOrderHtml(toPrintableWorkOrder(w, unit), vehicle, shop ? toPrintableShop(shop) : null, COMPANY),
    );
  }

  async function advance(w: WorkOrderWire, status: WorkOrderStatusWire) {
    if (rowBusy) return;
    setRowBusy(w.id);
    setActionError(null);
    try {
      await changeWorkOrderStatus(w.id, status);
      setReload((n) => n + 1);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Status change failed — please try again.");
    } finally {
      setRowBusy(null);
    }
  }

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>Work orders · {rows?.length ?? 0}</SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setNewOpen(true)}>
          + NEW WORK ORDER
        </ActionButton>
      </div>

      {actionError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600, marginBottom: 10 }}>
          ▲ {actionError}
        </div>
      )}

      {loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600, padding: "6px 2px" }}>
          ▲ {loadError}
        </div>
      ) : rows === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          Loading work orders…
        </div>
      ) : rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No work orders for {unit}.
        </div>
      ) : (
        rows.map((w) => {
          const isOpen = OPEN_WIRE_STATUSES.includes(w.status);
          const busy = rowBusy === w.id;
          return (
            <Panel key={w.id} style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                <MonoTag color={colors.skyBlue}>{w.number}</MonoTag>
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                  {w.title}
                </span>
                <StatusChip kind={workOrderKindWire(w)} label={`${WO_STATUS_LABEL[w.status]} · ${w.priority}`} />
                {w.source !== "Manual" && (
                  <MonoTag>
                    from {WO_SOURCE_LABEL[w.source]}
                    {w.sourceRef ? ` ${w.sourceRef}` : ""}
                  </MonoTag>
                )}
              </div>

              {w.description && (
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5, marginBottom: 8 }}>
                  {w.description}
                </div>
              )}

              {w.lineItems.length > 0 && (
                <ul style={{ margin: "0 0 8px", paddingLeft: 18 }}>
                  {w.lineItems.map((li, i) => (
                    <li key={i} style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}>
                      {li}
                    </li>
                  ))}
                </ul>
              )}

              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                  Created {formatUtcDate(w.createdAt)} by {w.createdBy}
                  {w.assignedTo ? ` · assigned ${w.assignedTo}` : ""}
                  {w.dueDate ? ` · due ${formatUtcDate(w.dueDate)}` : ""}
                  {w.completedAt ? ` · completed ${formatUtcDate(w.completedAt)}` : ""}
                </span>
                <div style={{ marginLeft: "auto", display: "flex", gap: 8, flexWrap: "wrap" }}>
                  <ActionButton onClick={() => printWorkOrder(w)}>PRINT WORK ORDER</ActionButton>
                  {isOpen && (
                    <>
                      {w.status === "Open" && (
                        <ActionButton disabled={busy} onClick={() => advance(w, "InProgress")}>
                          {busy ? "STARTING…" : "START"}
                        </ActionButton>
                      )}
                      {w.status !== "AwaitingParts" && (
                        <ActionButton disabled={busy} onClick={() => advance(w, "AwaitingParts")}>
                          {busy ? "UPDATING…" : "AWAIT PARTS"}
                        </ActionButton>
                      )}
                      <ActionButton variant="success" disabled={busy} onClick={() => setClosing(w)}>
                        COMPLETE → LOG SERVICE
                      </ActionButton>
                    </>
                  )}
                </div>
              </div>
            </Panel>
          );
        })
      )}

      {newOpen && (
        <WorkOrderModal
          vehicles={vehicles}
          defaultVehicleId={vehicleId}
          onClose={() => setNewOpen(false)}
          onSaved={() => setReload((n) => n + 1)}
        />
      )}
      {closing && (
        <ServiceRecordModal
          unit={unit}
          odometerKm={vehicle.odometerKm}
          closeWorkOrder={{ id: closing.id, number: closing.number, title: closing.title }}
          onClose={() => setClosing(null)}
          onSaved={() => setReload((n) => n + 1)}
        />
      )}
    </div>
  );
}
