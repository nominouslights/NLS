"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createPmPlan,
  getPmPlan,
  listPmPlans,
  seedDefaultPmPlan,
  updatePmPlan,
  type PmPlanInput,
  type PmPlanItemInput,
  type PmPlanOverhaulInput,
  type PmPlanSummaryWire,
  type PmPlanWire,
  type PmTaskWire,
  type PmTierWire,
} from "@/lib/api/pm";
import { formatShopMinutes, pmIntervalLabel, TASK_LABEL, TIER_LABEL } from "@/lib/pmDisplay";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { SectionLabel } from "@/components/ui/Panel";
import { dimText, errText, smallBtn } from "@/components/screens/fleet/vehicle-detail/shared";

// Maintenance-plan registry (ShopRegistryModal pattern): list the plans, seed
// the default, and create/edit a plan definition. A plan can carry ~250 lines,
// so the editor is deliberately pragmatic: compact read-only rows with
// per-row EDIT/REMOVE, one inline row editor open at a time, and a system
// filter over the items list — not a 250-row spreadsheet of live inputs.

const TIER_OPTIONS = (Object.keys(TIER_LABEL) as PmTierWire[]).map((t) => ({ value: t, label: TIER_LABEL[t] }));
const TASK_OPTIONS = (Object.keys(TASK_LABEL) as PmTaskWire[]).map((t) => ({ value: t, label: TASK_LABEL[t] }));

export default function PmPlansModal({ onClose }: { onClose: () => void }) {
  const [plans, setPlans] = useState<PmPlanSummaryWire[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [editing, setEditing] = useState<PmPlanWire | "new" | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listPmPlans().then(
      (rows) => {
        if (active) {
          setPlans(rows);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load maintenance plans.");
      },
    );
    return () => {
      active = false;
    };
  }, [reload]);

  async function openPlan(id: string) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      setEditing(await getPmPlan(id));
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to load the plan.");
    } finally {
      setBusy(false);
    }
  }

  async function seed() {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await seedDefaultPmPlan();
      setReload((n) => n + 1);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to seed the default plan.");
    } finally {
      setBusy(false);
    }
  }

  if (editing) {
    return (
      <PlanForm
        initial={editing === "new" ? null : editing}
        onCancel={() => setEditing(null)}
        onSaved={() => {
          setEditing(null);
          setReload((n) => n + 1);
        }}
      />
    );
  }

  return (
    <ModalShell
      eyebrow="Fleet & Maintenance · Preventive maintenance"
      title="PM Plans"
      onClose={onClose}
      error={actionError}
      maxWidth={700}
      footer={
        <>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          <ActionButton variant="primary" onClick={() => setEditing("new")}>
            + NEW PLAN
          </ActionButton>
        </>
      }
    >
      {loadError ? (
        <div style={errText}>▲ {loadError}</div>
      ) : plans === null ? (
        <div style={dimText}>Loading maintenance plans…</div>
      ) : plans.length === 0 ? (
        <div>
          <div style={{ ...dimText, marginBottom: 12 }}>
            No maintenance plans yet. Seed the built-in default plan (routine items + major-component
            overhauls) or build one from scratch.
          </div>
          <ActionButton variant="primary" disabled={busy} onClick={seed}>
            {busy ? "SEEDING…" : "SEED DEFAULT PLAN"}
          </ActionButton>
        </div>
      ) : (
        plans.map((p) => (
          <div
            key={p.id}
            style={{
              display: "grid",
              gridTemplateColumns: "1fr auto",
              gap: 10,
              alignItems: "center",
              padding: "11px 13px",
              marginBottom: 6,
              borderRadius: 9,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 2, flexWrap: "wrap" }}>
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                  {p.name}
                </span>
                <MonoTag color={colors.skyBlue}>{p.vehicleModel.toUpperCase()}</MonoTag>
                <MonoTag>{p.serviceClass.toUpperCase()}</MonoTag>
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                {p.itemCount} items · {p.overhaulCount} overhauls · assigned to {p.assignedVehicleCount}{" "}
                vehicle{p.assignedVehicleCount === 1 ? "" : "s"}
              </div>
            </div>
            <ActionButton disabled={busy} onClick={() => openPlan(p.id)}>
              EDIT
            </ActionButton>
          </div>
        ))
      )}
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Plan form — header fields + repeating item/overhaul row groups
// ---------------------------------------------------------------------------

interface ItemDraft {
  code: string;
  system: string;
  component: string;
  tier: PmTierWire;
  task: PmTaskWire;
  intervalKm: string;
  intervalMonths: string;
  shopMinutes: string;
  leadKm: string;
  leadDays: string;
  notes: string;
}

interface OverhaulDraft {
  code: string;
  component: string;
  intervalKm: string;
  intervalMonths: string;
  labourHours: string;
  partsCad: string;
  leadKm: string;
  leadDays: string;
  scope: string;
  triggers: string; // one per line
  relatedCodes: string; // comma-separated
}

const numStr = (n: number | null | undefined) => (n == null ? "" : String(n));

function emptyItemDraft(): ItemDraft {
  return { code: "", system: "", component: "", tier: "Primary", task: "Inspect", intervalKm: "", intervalMonths: "", shopMinutes: "", leadKm: "", leadDays: "", notes: "" };
}

function emptyOverhaulDraft(): OverhaulDraft {
  return { code: "", component: "", intervalKm: "", intervalMonths: "", labourHours: "", partsCad: "", leadKm: "", leadDays: "", scope: "", triggers: "", relatedCodes: "" };
}

function itemToDraft(i: PmPlanItemInput): ItemDraft {
  return {
    code: i.code,
    system: i.system,
    component: i.component,
    tier: i.tier,
    task: i.task,
    intervalKm: numStr(i.intervalKm),
    intervalMonths: numStr(i.intervalMonths),
    shopMinutes: String(i.shopMinutes),
    leadKm: numStr(i.leadKm),
    leadDays: numStr(i.leadDays),
    notes: i.notes ?? "",
  };
}

function overhaulToDraft(o: PmPlanOverhaulInput): OverhaulDraft {
  return {
    code: o.code,
    component: o.component,
    intervalKm: numStr(o.intervalKm),
    intervalMonths: numStr(o.intervalMonths),
    labourHours: String(o.labourHours),
    partsCad: String(o.partsCad),
    leadKm: numStr(o.leadKm),
    leadDays: numStr(o.leadDays),
    scope: o.scope,
    triggers: (o.conditionTriggers ?? []).join("\n"),
    relatedCodes: (o.relatedItemCodes ?? []).join(", "),
  };
}

/** "" → null; otherwise a positive whole number or undefined (invalid). The
 *  backend rejects any non-positive interval or lead, so row APPLY does too —
 *  a zero deferred to whole-plan save would surface as an unattributable error. */
function parsePosInt(s: string): number | null | undefined {
  const t = s.trim();
  if (!t) return null;
  const n = Number(t);
  return Number.isInteger(n) && n > 0 ? n : undefined;
}

/** Mirror of the backend's lead-vs-interval rules (MaintenancePlan.ValidateEntry):
 *  a lead at or past its own interval arm would pin the line to due-soon forever.
 *  28 days is the backend's conservative month floor. */
function leadError(
  intervalKm: number | null,
  intervalMonths: number | null,
  leadKm: number | null,
  leadDays: number | null,
): string | null {
  if (leadKm != null && intervalKm != null && leadKm >= intervalKm)
    return "The due-soon lead (km) must be smaller than the interval (km).";
  if (leadDays != null && intervalMonths != null && leadDays >= intervalMonths * 28)
    return "The due-soon lead (days) must be smaller than the calendar interval (months × 28 days).";
  return null;
}

function draftToItem(d: ItemDraft): { item?: PmPlanItemInput; error?: string } {
  if (!d.code.trim()) return { error: "Each item needs a code." };
  if (!d.system.trim()) return { error: "Each item needs a system." };
  if (!d.component.trim()) return { error: "Each item needs a component." };
  const intervalKm = parsePosInt(d.intervalKm);
  const intervalMonths = parsePosInt(d.intervalMonths);
  const leadKm = parsePosInt(d.leadKm);
  const leadDays = parsePosInt(d.leadDays);
  if (intervalKm === undefined || intervalMonths === undefined || leadKm === undefined || leadDays === undefined)
    return { error: "Intervals and leads must be whole numbers greater than zero (leave blank for n/a)." };
  if (intervalKm == null && intervalMonths == null)
    return { error: "Each item needs at least one interval arm (km or months)." };
  const lead = leadError(intervalKm, intervalMonths, leadKm, leadDays);
  if (lead) return { error: lead };
  const shopMinutes = Number(d.shopMinutes.trim());
  if (!d.shopMinutes.trim() || !Number.isInteger(shopMinutes) || shopMinutes <= 0)
    return { error: "Each item needs its shop minutes — a whole number greater than zero." };
  return {
    item: {
      code: d.code.trim(),
      system: d.system.trim(),
      component: d.component.trim(),
      tier: d.tier,
      task: d.task,
      intervalKm,
      intervalMonths,
      shopMinutes,
      leadKm,
      leadDays,
      notes: d.notes.trim() || null,
    },
  };
}

function draftToOverhaul(d: OverhaulDraft): { overhaul?: PmPlanOverhaulInput; error?: string } {
  if (!d.code.trim()) return { error: "Each overhaul needs a code." };
  if (!d.component.trim()) return { error: "Each overhaul needs a component." };
  if (!d.scope.trim()) return { error: "Each overhaul needs its scope of work." };
  const intervalKm = parsePosInt(d.intervalKm);
  const intervalMonths = parsePosInt(d.intervalMonths);
  const leadKm = parsePosInt(d.leadKm);
  const leadDays = parsePosInt(d.leadDays);
  if (intervalKm === undefined || intervalMonths === undefined || leadKm === undefined || leadDays === undefined)
    return { error: "Intervals and leads must be whole numbers greater than zero (leave blank for n/a)." };
  if (intervalKm == null && intervalMonths == null)
    return { error: "Each overhaul needs at least one interval arm (km or months)." };
  const lead = leadError(intervalKm, intervalMonths, leadKm, leadDays);
  if (lead) return { error: lead };
  const labourHours = Number(d.labourHours.trim());
  if (!d.labourHours.trim() || !Number.isFinite(labourHours) || labourHours <= 0)
    return { error: "Each overhaul needs its labour hours — greater than zero." };
  const partsCad = Number(d.partsCad.trim());
  if (!d.partsCad.trim() || !Number.isFinite(partsCad) || partsCad < 0)
    return { error: "Each overhaul needs its estimated parts cost (CAD)." };
  return {
    overhaul: {
      code: d.code.trim(),
      component: d.component.trim(),
      intervalKm,
      intervalMonths,
      labourHours,
      partsCad,
      leadKm,
      leadDays,
      scope: d.scope.trim(),
      conditionTriggers: d.triggers
        .split("\n")
        .map((t) => t.trim())
        .filter(Boolean),
      relatedItemCodes: d.relatedCodes
        .split(",")
        .map((t) => t.trim())
        .filter(Boolean),
    },
  };
}

// The open row editor targets a row by a generated stable KEY, never by array
// index — removing a row above the edited one shifts every index, and an
// index-tracked editor would then APPLY onto the wrong row (or silently drop
// the edit). Keys are generated because codes are user-editable and drafts may
// briefly duplicate; "new" never collides with the "row-N" key shape.
let rowKeyCounter = 0;
const newRowKey = () => `row-${++rowKeyCounter}`;

interface KeyedRow<T> {
  key: string;
  value: T;
}

const keyed = <T,>(value: T): KeyedRow<T> => ({ key: newRowKey(), value });

type RowEdit = { kind: "item" | "overhaul"; key: string | "new" } | null;

function PlanForm({
  initial,
  onCancel,
  onSaved,
}: {
  initial: PmPlanWire | null;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [f, setF] = useState({
    name: initial?.name ?? "",
    vehicleModel: initial?.vehicleModel ?? "",
    serviceClass: initial?.serviceClass ?? "",
    notes: initial?.notes ?? "",
  });
  const [items, setItems] = useState<KeyedRow<PmPlanItemInput>[]>(() => (initial?.items ?? []).map((i) => keyed({ ...i })));
  const [overhauls, setOverhauls] = useState<KeyedRow<PmPlanOverhaulInput>[]>(() =>
    (initial?.overhauls ?? []).map((o) => keyed({ ...o })),
  );

  const [rowEdit, setRowEdit] = useState<RowEdit>(null);
  const [itemDraft, setItemDraft] = useState<ItemDraft>(emptyItemDraft());
  const [overhaulDraft, setOverhaulDraft] = useState<OverhaulDraft>(emptyOverhaulDraft());
  const [rowError, setRowError] = useState<string | null>(null);
  const [sysFilter, setSysFilter] = useState("all");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = <K extends keyof typeof f>(key: K, value: (typeof f)[K]) => setF((prev) => ({ ...prev, [key]: value }));
  const setI = <K extends keyof ItemDraft>(key: K, value: ItemDraft[K]) =>
    setItemDraft((prev) => ({ ...prev, [key]: value }));
  const setO = <K extends keyof OverhaulDraft>(key: K, value: OverhaulDraft[K]) =>
    setOverhaulDraft((prev) => ({ ...prev, [key]: value }));

  function startItemEdit(key: string | "new") {
    if (key === "new") {
      setItemDraft(emptyItemDraft());
    } else {
      const row = items.find((r) => r.key === key);
      if (!row) return;
      setItemDraft(itemToDraft(row.value));
    }
    setRowEdit({ kind: "item", key });
    setRowError(null);
  }

  function startOverhaulEdit(key: string | "new") {
    if (key === "new") {
      setOverhaulDraft(emptyOverhaulDraft());
    } else {
      const row = overhauls.find((r) => r.key === key);
      if (!row) return;
      setOverhaulDraft(overhaulToDraft(row.value));
    }
    setRowEdit({ kind: "overhaul", key });
    setRowError(null);
  }

  function applyRow() {
    if (!rowEdit) return;
    if (rowEdit.kind === "item") {
      const { item, error: e } = draftToItem(itemDraft);
      if (!item) return setRowError(e ?? "Invalid item.");
      setItems((prev) =>
        rowEdit.key === "new" ? [...prev, keyed(item)] : prev.map((r) => (r.key === rowEdit.key ? { ...r, value: item } : r)),
      );
    } else {
      const { overhaul, error: e } = draftToOverhaul(overhaulDraft);
      if (!overhaul) return setRowError(e ?? "Invalid overhaul.");
      setOverhauls((prev) =>
        rowEdit.key === "new"
          ? [...prev, keyed(overhaul)]
          : prev.map((r) => (r.key === rowEdit.key ? { ...r, value: overhaul } : r)),
      );
    }
    setRowEdit(null);
    setRowError(null);
  }

  async function submit() {
    if (busy) return;
    if (!f.name.trim()) return setError("Enter the plan name.");
    if (!f.vehicleModel.trim()) return setError("Enter the vehicle model the plan applies to.");
    if (!f.serviceClass.trim()) return setError("Enter the service class (e.g. Severe — gravel/winter).");
    if (rowEdit) return setError("Apply or cancel the open row editor first.");
    const input: PmPlanInput = {
      name: f.name.trim(),
      vehicleModel: f.vehicleModel.trim(),
      serviceClass: f.serviceClass.trim(),
      notes: f.notes.trim() || null,
      items: items.map((r) => r.value),
      overhauls: overhauls.map((r) => r.value),
    };
    setBusy(true);
    setError(null);
    try {
      if (initial) await updatePmPlan(initial.id, input);
      else await createPmPlan(input);
      onSaved();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to save the plan — please try again.");
    }
  }

  const systems = Array.from(new Set(items.map((r) => r.value.system)));
  // Removing (or renaming) the last item of the filtered system would leave
  // the select holding a value absent from its options and the list empty for
  // no visible reason — snap back to "All" (render-phase state adjustment).
  if (sysFilter !== "all" && !systems.includes(sysFilter)) {
    setSysFilter("all");
  }
  const visibleItems = items.filter((r) => sysFilter === "all" || r.value.system === sysFilter);

  const rowStyle = {
    display: "grid",
    gridTemplateColumns: "64px 1fr auto",
    gap: 10,
    alignItems: "center",
    padding: "8px 11px",
    marginBottom: 5,
    borderRadius: 9,
    border: `1px solid ${colors.borderSubtle}`,
    background: colors.cardBg,
  } as const;

  return (
    <ModalShell
      eyebrow="Fleet & Maintenance · Preventive maintenance"
      title={initial ? `Edit ${initial.name}` : "New PM Plan"}
      onClose={onCancel}
      error={error}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onCancel} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : initial ? "SAVE PLAN" : "CREATE PLAN"}
          </ActionButton>
        </>
      }
    >
      {/* plan header */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <TextField label="Plan name" value={f.name} onChange={(v) => set("name", v)} placeholder="Ford Transit — Severe Service" />
        <TextField label="Vehicle model" value={f.vehicleModel} onChange={(v) => set("vehicleModel", v)} placeholder="Ford Transit T-350" />
        <TextField label="Service class" value={f.serviceClass} onChange={(v) => set("serviceClass", v)} placeholder="Severe — gravel / winter roads" />
        <TextField label="Notes (optional)" value={f.notes} onChange={(v) => set("notes", v)} />
      </div>

      {/* items */}
      <div style={{ borderTop: `1px solid ${colors.border}`, paddingTop: 14, marginBottom: 14 }}>
        <div style={{ display: "flex", alignItems: "flex-end", gap: 10, marginBottom: 10 }}>
          <SectionLabel>Routine items · {items.length}</SectionLabel>
          <div style={{ marginLeft: "auto", display: "flex", gap: 9, alignItems: "flex-end" }}>
            {systems.length > 1 && (
              <div style={{ width: 200 }}>
                <SelectField
                  label="Filter by system"
                  value={sysFilter}
                  onChange={setSysFilter}
                  options={[{ value: "all", label: "All systems" }, ...systems.map((s) => ({ value: s, label: s }))]}
                />
              </div>
            )}
            <ActionButton style={smallBtn} onClick={() => startItemEdit("new")}>
              + ADD ITEM
            </ActionButton>
          </div>
        </div>

        {items.length === 0 && <div style={{ ...dimText, marginBottom: 8 }}>No routine items yet.</div>}

        {visibleItems.map(({ key, value: item }) => (
          <div key={key} style={rowStyle}>
            <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{item.code}</span>
            <div style={{ minWidth: 0 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 7, minWidth: 0 }}>
                <span
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    fontWeight: 600,
                    color: colors.textPrimary,
                    whiteSpace: "nowrap",
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                  }}
                >
                  {item.component}
                </span>
                <MonoTag>{TIER_LABEL[item.tier].toUpperCase()}</MonoTag>
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                {item.system} · {TASK_LABEL[item.task]} · every{" "}
                {pmIntervalLabel({ intervalKm: item.intervalKm ?? null, intervalMonths: item.intervalMonths ?? null })} ·{" "}
                {formatShopMinutes(item.shopMinutes)}
              </div>
            </div>
            <div style={{ display: "flex", gap: 7 }}>
              <ActionButton style={smallBtn} onClick={() => startItemEdit(key)}>
                EDIT
              </ActionButton>
              <ActionButton
                style={smallBtn}
                variant="destructive"
                onClick={() => {
                  setItems((prev) => prev.filter((r) => r.key !== key));
                  // Removing any row leaves other keys intact; only close the
                  // editor when the removed row is the one being edited.
                  if (rowEdit?.kind === "item" && rowEdit.key === key) setRowEdit(null);
                }}
              >
                REMOVE
              </ActionButton>
            </div>
          </div>
        ))}

        {rowEdit?.kind === "item" && (
          <div
            style={{
              marginTop: 10,
              padding: "13px 14px",
              borderRadius: 10,
              border: `1px solid ${colors.borderActive}`,
              background: colors.inputBg,
            }}
          >
            <SectionLabel>{rowEdit.key === "new" ? "Add routine item" : `Edit item ${itemDraft.code || ""}`}</SectionLabel>
            {rowError && <div style={{ ...errText, marginBottom: 10 }}>▲ {rowError}</div>}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
              <TextField label="Code" value={itemDraft.code} onChange={(v) => setI("code", v)} mono placeholder="ENG-01" />
              <TextField label="System" value={itemDraft.system} onChange={(v) => setI("system", v)} placeholder="Engine" />
              <TextField label="Component" value={itemDraft.component} onChange={(v) => setI("component", v)} placeholder="Engine oil & filter" />
              <SelectField label="Tier" value={itemDraft.tier} onChange={(v) => setI("tier", v as PmTierWire)} options={TIER_OPTIONS} />
              <SelectField label="Task" value={itemDraft.task} onChange={(v) => setI("task", v as PmTaskWire)} options={TASK_OPTIONS} />
              <NumberField label="Shop minutes" value={itemDraft.shopMinutes} onChange={(v) => setI("shopMinutes", v)} min={0} step={5} />
              <NumberField label="Interval (km)" value={itemDraft.intervalKm} onChange={(v) => setI("intervalKm", v)} min={0} step={500} hint={<span style={{ color: colors.textFaint }}>· blank = n/a</span>} />
              <NumberField label="Interval (months)" value={itemDraft.intervalMonths} onChange={(v) => setI("intervalMonths", v)} min={0} step={1} hint={<span style={{ color: colors.textFaint }}>· blank = n/a</span>} />
              <NumberField label="Lead (km)" value={itemDraft.leadKm} onChange={(v) => setI("leadKm", v)} min={0} step={100} hint={<span style={{ color: colors.textFaint }}>· blank = default</span>} />
              <NumberField label="Lead (days)" value={itemDraft.leadDays} onChange={(v) => setI("leadDays", v)} min={0} step={1} hint={<span style={{ color: colors.textFaint }}>· blank = default</span>} />
            </div>
            <div style={{ marginTop: 12 }}>
              <TextField label="Notes (optional)" value={itemDraft.notes} onChange={(v) => setI("notes", v)} />
            </div>
            <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
              <ActionButton variant="primary" style={smallBtn} onClick={applyRow}>
                {rowEdit.key === "new" ? "ADD ITEM" : "APPLY"}
              </ActionButton>
              <ActionButton style={smallBtn} onClick={() => setRowEdit(null)}>
                CANCEL
              </ActionButton>
            </div>
          </div>
        )}
      </div>

      {/* overhauls */}
      <div style={{ borderTop: `1px solid ${colors.border}`, paddingTop: 14 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
          <SectionLabel>Major-component overhauls · {overhauls.length}</SectionLabel>
          <ActionButton style={{ ...smallBtn, marginLeft: "auto" }} onClick={() => startOverhaulEdit("new")}>
            + ADD OVERHAUL
          </ActionButton>
        </div>

        {overhauls.length === 0 && <div style={{ ...dimText, marginBottom: 8 }}>No overhauls yet.</div>}

        {overhauls.map(({ key, value: o }) => (
          <div key={key} style={rowStyle}>
            <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{o.code}</span>
            <div style={{ minWidth: 0 }}>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: 12.5,
                  fontWeight: 600,
                  color: colors.textPrimary,
                  whiteSpace: "nowrap",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                }}
              >
                {o.component}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                every {pmIntervalLabel({ intervalKm: o.intervalKm ?? null, intervalMonths: o.intervalMonths ?? null })} ·{" "}
                {o.labourHours} h labour · parts ${o.partsCad.toLocaleString("en-CA")} ·{" "}
                {(o.conditionTriggers ?? []).length} trigger{(o.conditionTriggers ?? []).length === 1 ? "" : "s"}
              </div>
            </div>
            <div style={{ display: "flex", gap: 7 }}>
              <ActionButton style={smallBtn} onClick={() => startOverhaulEdit(key)}>
                EDIT
              </ActionButton>
              <ActionButton
                style={smallBtn}
                variant="destructive"
                onClick={() => {
                  setOverhauls((prev) => prev.filter((r) => r.key !== key));
                  if (rowEdit?.kind === "overhaul" && rowEdit.key === key) setRowEdit(null);
                }}
              >
                REMOVE
              </ActionButton>
            </div>
          </div>
        ))}

        {rowEdit?.kind === "overhaul" && (
          <div
            style={{
              marginTop: 10,
              padding: "13px 14px",
              borderRadius: 10,
              border: `1px solid ${colors.borderActive}`,
              background: colors.inputBg,
            }}
          >
            <SectionLabel>
              {rowEdit.key === "new" ? "Add overhaul" : `Edit overhaul ${overhaulDraft.code || ""}`}
            </SectionLabel>
            {rowError && <div style={{ ...errText, marginBottom: 10 }}>▲ {rowError}</div>}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
              <TextField label="Code" value={overhaulDraft.code} onChange={(v) => setO("code", v)} mono placeholder="OVH-01" />
              <TextField label="Component" value={overhaulDraft.component} onChange={(v) => setO("component", v)} placeholder="Transmission" />
              <NumberField label="Labour (hours)" value={overhaulDraft.labourHours} onChange={(v) => setO("labourHours", v)} min={0} step={0.5} />
              <NumberField label="Parts (CAD)" value={overhaulDraft.partsCad} onChange={(v) => setO("partsCad", v)} min={0} step={100} />
              <NumberField label="Interval (km)" value={overhaulDraft.intervalKm} onChange={(v) => setO("intervalKm", v)} min={0} step={1000} hint={<span style={{ color: colors.textFaint }}>· blank = n/a</span>} />
              <NumberField label="Interval (months)" value={overhaulDraft.intervalMonths} onChange={(v) => setO("intervalMonths", v)} min={0} step={1} hint={<span style={{ color: colors.textFaint }}>· blank = n/a</span>} />
              <NumberField label="Lead (km)" value={overhaulDraft.leadKm} onChange={(v) => setO("leadKm", v)} min={0} step={100} hint={<span style={{ color: colors.textFaint }}>· blank = default</span>} />
              <NumberField label="Lead (days)" value={overhaulDraft.leadDays} onChange={(v) => setO("leadDays", v)} min={0} step={1} hint={<span style={{ color: colors.textFaint }}>· blank = default</span>} />
              <TextField label="Related item codes" value={overhaulDraft.relatedCodes} onChange={(v) => setO("relatedCodes", v)} mono placeholder="TRN-02, TRN-03" hint={<span style={{ color: colors.textFaint }}>· comma-separated</span>} />
            </div>
            <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 12 }}>
              <TextAreaField label="Scope of work" value={overhaulDraft.scope} onChange={(v) => setO("scope", v)} rows={2} />
              <TextAreaField
                label="Condition triggers (one per line)"
                value={overhaulDraft.triggers}
                onChange={(v) => setO("triggers", v)}
                rows={3}
                placeholder={"Slipping between gears under load\nFluid dark or burnt at service"}
              />
            </div>
            <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
              <ActionButton variant="primary" style={smallBtn} onClick={applyRow}>
                {rowEdit.key === "new" ? "ADD OVERHAUL" : "APPLY"}
              </ActionButton>
              <ActionButton style={smallBtn} onClick={() => setRowEdit(null)}>
                CANCEL
              </ActionButton>
            </div>
          </div>
        )}
      </div>
    </ModalShell>
  );
}
