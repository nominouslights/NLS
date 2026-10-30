"use client";

import { Fragment, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { shortDateLabel } from "@/lib/api/trips";
import type { BookeoImportGroup, BookeoImportPreview, BookeoImportRow } from "@/lib/api/bookeoImport";
import {
  formatCadCents,
  groupActionChip,
  groupRows,
  rowActionChip,
  vehicleMatchChip,
} from "@/lib/bookeoImport";
import { IssueChips, IssueList, SpecChip, mono, muted, td, th } from "./shared";

// One row per planned trip (route + direction + date + departure). A row
// expands to the bookings it carries. Bookings that land on no group (an
// unmapped product, an unreadable row, a cancellation never imported) are
// listed underneath so nothing in the file goes unaccounted for.

function directionLabel(d: string | null): string {
  if (d === "Outbound") return "Outbound";
  if (d === "Inbound") return "Inbound";
  return "";
}

export function GroupsTable({ preview }: { preview: BookeoImportPreview }) {
  const [open, setOpen] = useState<Set<string>>(() => new Set());
  const { byGroup, ungrouped } = groupRows(preview);

  function toggle(key: string) {
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {preview.groups.length === 0 ? (
        <div style={muted}>No trips in this file.</div>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", minWidth: 900 }}>
            <thead>
              <tr>
                <th style={{ ...th, width: 18 }} aria-label="Expand" />
                <th style={th}>Date</th>
                <th style={th}>Time</th>
                <th style={th}>Route</th>
                <th style={th}>Action</th>
                <th style={th}>Trip #</th>
                <th style={th}>Vehicle</th>
                <th style={{ ...th, textAlign: "right" }}>Passengers</th>
                <th style={th}>Issues</th>
              </tr>
            </thead>
            <tbody>
              {preview.groups.map((g) => (
                <GroupRow
                  key={g.key}
                  group={g}
                  rows={byGroup.get(g.key) ?? []}
                  expanded={open.has(g.key)}
                  onToggle={() => toggle(g.key)}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}

      {ungrouped.length > 0 && (
        <div>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".14em",
              textTransform: "uppercase",
              color: colors.textLabel,
              marginBottom: 8,
            }}
          >
            Bookings not placed on a trip ({ungrouped.length})
          </div>
          <BookingsTable rows={ungrouped} />
        </div>
      )}
    </div>
  );
}

function GroupRow({
  group: g,
  rows,
  expanded,
  onToggle,
}: {
  group: BookeoImportGroup;
  rows: BookeoImportRow[];
  expanded: boolean;
  onToggle: () => void;
}) {
  const vehicleText = g.vehicle.vehicleUnit ?? g.vehicle.unitText ?? "—";
  const paxDelta = g.passengersAfter - g.passengersBefore;
  return (
    <Fragment>
      <tr
        onClick={onToggle}
        aria-expanded={expanded}
        style={{ cursor: "pointer", background: expanded ? colors.cardBgActive : undefined }}
      >
        <td style={{ ...td, color: colors.textDim, fontSize: 11 }}>{expanded ? "▾" : "▸"}</td>
        <td style={{ ...td, whiteSpace: "nowrap" }}>{shortDateLabel(g.serviceDate)}</td>
        <td style={{ ...td, ...mono, whiteSpace: "nowrap" }}>
          {g.windowStart}
          {g.windowEnd ? `–${g.windowEnd}` : ""}
        </td>
        <td style={td}>
          <div style={{ fontWeight: 500 }}>{g.routeName ?? "No route"}</div>
          {g.direction && <div style={{ ...muted, fontSize: 11.5 }}>{directionLabel(g.direction)}</div>}
        </td>
        <td style={td}>
          <SpecChip spec={groupActionChip(g.action)} />
        </td>
        <td style={{ ...td, ...mono }}>{g.existingTripNumber ?? (g.action === "Create" ? "new" : "—")}</td>
        <td style={td}>
          <div style={{ display: "flex", flexDirection: "column", gap: 3, alignItems: "flex-start" }}>
            <span>{vehicleText}</span>
            <SpecChip spec={vehicleMatchChip(g.vehicle.match)} />
            {g.driverName && <span style={{ ...muted, fontSize: 11.5 }}>Driver: {g.driverName}</span>}
          </div>
        </td>
        <td style={{ ...td, ...mono, textAlign: "right", whiteSpace: "nowrap" }}>
          {g.passengersBefore} → {g.passengersAfter}
          {paxDelta !== 0 && (
            <div style={{ color: colors.textDim, fontSize: 10.5 }}>
              {paxDelta > 0 ? `+${paxDelta}` : paxDelta}
            </div>
          )}
          {g.seatsCapacity !== null && (
            <div style={{ color: colors.textDim, fontSize: 10.5 }}>of {g.seatsCapacity} seats</div>
          )}
        </td>
        <td style={td}>
          <IssueChips issues={g.issues} />
        </td>
      </tr>
      {expanded && (
        <tr>
          <td colSpan={9} style={{ padding: "4px 8px 14px 26px", background: colors.detailBg }}>
            {g.issues.length > 0 && (
              <div style={{ margin: "8px 0 12px" }}>
                <IssueList issues={g.issues} />
              </div>
            )}
            {rows.length === 0 ? (
              <div style={{ ...muted, paddingTop: 8 }}>No bookings in this file for this trip.</div>
            ) : (
              <BookingsTable rows={rows} />
            )}
          </td>
        </tr>
      )}
    </Fragment>
  );
}

export function BookingsTable({ rows }: { rows: BookeoImportRow[] }) {
  const right = { ...th, textAlign: "right" as const };
  return (
    <div style={{ overflowX: "auto", paddingTop: 6 }}>
      <table style={{ width: "100%", borderCollapse: "collapse", minWidth: 860 }}>
        <thead>
          <tr>
            <th style={th}>Booking #</th>
            <th style={th}>Customer</th>
            <th style={right}>Pax</th>
            <th style={th}>Status</th>
            <th style={right} title="Bookeo's Total gross — tax-inclusive">
              Gross (tax-inclusive)
            </th>
            <th style={right}>Paid</th>
            <th style={right}>Due</th>
            <th style={th}>Action</th>
            <th style={th}>Changed</th>
            <th style={th}>Issues</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.bookingNumber}>
              <td style={{ ...td, ...mono }}>{r.bookingNumber}</td>
              <td style={td}>
                <div>{r.customerName}</div>
                <div style={{ ...muted, fontSize: 11 }}>
                  {r.productName}
                  {r.destination ? ` · ${r.destination}` : ""}
                </div>
              </td>
              <td style={{ ...td, ...mono, textAlign: "right" }}>{r.participants}</td>
              <td style={{ ...td, textTransform: "capitalize", whiteSpace: "nowrap" }}>{r.bookeoStatus}</td>
              <td style={{ ...td, ...mono, textAlign: "right" }}>{formatCadCents(r.totalGrossCad)}</td>
              <td style={{ ...td, ...mono, textAlign: "right" }}>{formatCadCents(r.totalPaidCad)}</td>
              <td style={{ ...td, ...mono, textAlign: "right" }}>{formatCadCents(r.totalDueCad)}</td>
              <td style={td}>
                <SpecChip spec={rowActionChip(r.action)} />
              </td>
              <td style={{ ...td, fontSize: 11.5, color: colors.textSecondary }}>
                {r.changedFields.length > 0 ? r.changedFields.join(", ") : "—"}
              </td>
              <td style={td}>
                <IssueChips issues={r.issues} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
