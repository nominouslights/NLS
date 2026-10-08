"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  createCostCentre,
  listCostCentres,
  ownerLabel,
  refetchUntil,
  updateCostCentre,
  type BudgetOwnerOption,
  type CostCentreRecord,
} from "@/lib/api/budgeting";
import {
  COST_CENTRE_LIMITS,
  costCentreError,
  costCentreReflects,
  hasChildren,
  normalizeCostCentreCode,
  normalizeCostCentreText,
  parentCandidates,
} from "@/lib/costCentres";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create-and-edit modal for one entry of the cost-centre register, following BudgetCodeFormModal:
// one useState per field, the server is authoritative, and its refusal shows verbatim in the
// vermillion banner.
//
// The register is TENANT-WIDE, so unlike the code and item modals this one names no period and
// takes no period hold — nothing it writes belongs to the period the banner names. It opens from
// the Cost Centres screen and, inline, from the budget-code form's "+ New cost centre…".
//
// Rules carried visibly, because the server enforces them and none is guessable:
//   1. The code is set once, trimmed, CASE PRESERVED — budget codes carry it by that string. In
//      edit mode it is read-only text, never a disabled input ("not ever", not "not right now").
//   2. The hierarchy is one level deep (lib/costCentres parentCandidates mirrors
//      CostCentreParentRule): only active top-level entries are offered, the current parent is
//      kept even if retired since, and an entry that already has children gets no parent at all.

/** The requests the modal makes. A prop so a component test can inject vi.fn()s. */
export interface CostCentreApi {
  create: typeof createCostCentre;
  update: typeof updateCostCentre;
  list: typeof listCostCentres;
}

const DEFAULT_API: CostCentreApi = {
  create: createCostCentre,
  update: updateCostCentre,
  list: listCostCentres,
};

const NONE = "";

export default function CostCentreFormModal({
  entry,
  register,
  owners,
  onClose,
  onSaved,
  eyebrow = "Cost Centres",
  api = DEFAULT_API,
}: {
  /** null → create; an entry → edit (its code is fixed). */
  entry: CostCentreRecord | null;
  /** The WHOLE register, retired entries included — parentCandidates needs them. */
  register: CostCentreRecord[];
  owners: BudgetOwnerOption[];
  onClose: () => void;
  /** The fresh register (includeInactive), already showing the change, plus the entry's id. */
  onSaved: (records: CostCentreRecord[], id: string) => void;
  eyebrow?: string;
  api?: CostCentreApi;
}) {
  const editing = entry !== null;

  const [codeText, setCodeText] = useState("");
  const [name, setName] = useState(entry?.name ?? "");
  const [description, setDescription] = useState(entry?.description ?? "");
  const [ownerUserId, setOwnerUserId] = useState<string>(entry?.ownerUserId ?? NONE);
  const [parentId, setParentId] = useState<string>(entry?.parentId ?? NONE);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const close = () => {
    if (!busy) onClose();
  };

  const parents = parentCandidates(register, entry?.id ?? null);
  const isParent = editing && hasChildren(register, entry.id);
  const parentOptions = [
    { value: NONE, label: "— Top level —" },
    ...parents.map((c) => ({
      value: c.id,
      label: `${c.code} · ${c.name}${c.isActive ? "" : " (retired — current parent)"}`,
    })),
  ];

  const ownerOptions = [
    { value: NONE, label: "— Unassigned —" },
    ...owners.map((o) => ({ value: o.userId, label: ownerLabel(o) })),
  ];
  // An owner the replica no longer lists stays selectable rather than silently clearing.
  if (entry?.ownerUserId && !owners.some((o) => o.userId === entry.ownerUserId)) {
    ownerOptions.push({
      value: entry.ownerUserId,
      label: entry.ownerName?.trim() || entry.ownerEmail || "Current owner",
    });
  }

  async function submit() {
    if (busy) return;

    const details = {
      name: name.trim(),
      description: normalizeCostCentreText(description),
      ownerUserId: ownerUserId || null,
      parentId: parentId || null,
    };
    const code = normalizeCostCentreCode(codeText);

    const refused = costCentreError({ code, ...details }, entry?.id ?? null);
    if (refused) return setError(refused);

    setBusy(true);
    setError(null);
    try {
      let id: string;
      if (editing) {
        id = entry.id;
        await api.update(id, details);
      } else {
        id = await api.create({ code, ...details });
      }
      // Reads trail writes — refetch until THIS entry carries the values just written.
      const records = await refetchUntil(
        () => api.list({ includeInactive: true }),
        (rows) => costCentreReflects(rows, id, details),
      );
      onSaved(records, id);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? e.message
          : `Failed to ${editing ? "save the" : "create the"} cost centre — please try again.`,
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={eyebrow}
      title={editing ? `Edit ${entry.code}` : "New Cost Centre"}
      onClose={close}
      error={error}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={close} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "CREATE COST CENTRE"}
          </ActionButton>
        </>
      }
    >
      <Row>
        {editing ? (
          <FixedCode code={entry.code} />
        ) : (
          <TextField
            label="Code"
            value={codeText}
            onChange={setCodeText}
            mono
            maxLength={COST_CENTRE_LIMITS.codeMaxLength}
            placeholder="THOMPSON"
            hint="Set once — saved exactly as typed, case kept"
          />
        )}
        <TextField
          label="Name"
          value={name}
          onChange={setName}
          maxLength={COST_CENTRE_LIMITS.nameMaxLength}
          placeholder="Thompson base"
        />
      </Row>

      <Row top>
        <SelectField
          label="Parent"
          value={parentId}
          onChange={setParentId}
          options={parentOptions}
          disabled={isParent}
          hint={
            isParent
              ? "Others roll up into this one, so it stays top level — the hierarchy is one level deep"
              : "Only active top-level cost centres can be parents"
          }
        />
        <SelectField
          label="Owner"
          value={ownerUserId}
          onChange={setOwnerUserId}
          options={ownerOptions}
          hint="Accountable person"
        />
      </Row>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Description"
          value={description}
          onChange={setDescription}
          rows={3}
          placeholder="Which unit or base this is, and what cost lands here."
        />
      </div>

      <div style={{ marginTop: 10, fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        A cost centre is an organisational unit or base, shared by every period. Budget codes carry
        it by its code; retiring it later stops it being offered for new codes without disturbing
        the codes that already carry it.
      </div>
    </ModalShell>
  );
}

function Row({ children, top = false }: { children: React.ReactNode; top?: boolean }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: top ? 14 : 0 }}>
      {children}
    </div>
  );
}

/** The code in edit mode — read-only text, because it is permanent. */
function FixedCode({ code }: { code: string }) {
  return (
    <div>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 6,
        }}
      >
        Code
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.textPrimary, padding: "8px 0" }}>
        {code}
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        Cannot be changed — retire this cost centre and create a new one instead.
      </div>
    </div>
  );
}
