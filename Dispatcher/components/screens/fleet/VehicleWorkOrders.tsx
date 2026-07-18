"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { WorkOrder } from "@/lib/types";
import { shops, updateWorkOrder, useMaintenanceStore, workOrders } from "@/lib/maintenanceStore";
import { formatUtcDate, type Vehicle } from "@/lib/api";
import { COMPANY } from "@/lib/company";
import { openPrintDocument } from "@/lib/documents/printDocument";
import { workOrderHtml } from "@/lib/documents/workOrderPdf";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import WorkOrderModal from "@/components/WorkOrderModal";
import ServiceRecordModal from "@/components/ServiceRecordModal";

const OPEN_STATUSES = ["Open", "In Progress", "Awaiting Parts"];

// Per-vehicle work orders. Create manually, advance status, close by logging the
// service that resolved it, and print the NL-WO-01 form to take to the shop.

export default function VehicleWorkOrders({
  vehicle,
  units,
}: {
  vehicle: Vehicle;
  units: { value: string; label: string }[];
}) {
  useMaintenanceStore();
  const [newOpen, setNewOpen] = useState(false);
  const [closing, setClosing] = useState<WorkOrder | null>(null);

  const unit = vehicle.unitNumber;
  const rows = workOrders.filter((w) => w.unit === unit);

  function printWorkOrder(wo: WorkOrder) {
    const shop = shops.find((s) => s.id === wo.shopId) ?? null;
    openPrintDocument(`Work Order ${wo.id}`, workOrderHtml(wo, vehicle, shop, COMPANY));
  }

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>Work orders · {rows.length}</SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setNewOpen(true)}>
          + NEW WORK ORDER
        </ActionButton>
      </div>

      {rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No work orders for {unit}.
        </div>
      ) : (
        rows.map((w) => {
          const isOpen = OPEN_STATUSES.includes(w.status);
          return (
            <Panel key={w.id} style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                <MonoTag color={colors.skyBlue}>{w.id}</MonoTag>
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                  {w.title}
                </span>
                <StatusChip kind={w.k} label={`${w.status} · ${w.priority}`} />
                {w.source !== "Manual" && (
                  <MonoTag>
                    from {w.source}
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
                        <ActionButton onClick={() => updateWorkOrder(w.id, { status: "In Progress" })}>START</ActionButton>
                      )}
                      {w.status !== "Awaiting Parts" && (
                        <ActionButton onClick={() => updateWorkOrder(w.id, { status: "Awaiting Parts" })}>
                          AWAIT PARTS
                        </ActionButton>
                      )}
                      <ActionButton variant="success" onClick={() => setClosing(w)}>
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
          units={units}
          defaultUnit={unit}
          onClose={() => setNewOpen(false)}
          onSaved={() => undefined}
        />
      )}
      {closing && (
        <ServiceRecordModal
          unit={unit}
          odometerKm={vehicle.odometerKm}
          closeWorkOrderId={closing.id}
          closeWorkOrderTitle={closing.title}
          onClose={() => setClosing(null)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}
