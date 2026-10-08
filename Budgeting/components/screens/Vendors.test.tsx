import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import Vendors, { type VendorsApi } from "@/components/screens/Vendors";
import VendorFormModal from "@/components/VendorFormModal";
import { ApiError } from "@/lib/api/transport";
import type { VendorRecord } from "@/lib/api/budgeting";

// The Vendors screen and its create/edit modal both take their requests as an `api` prop, so this
// drives them with vi.fn()s rather than stubbing the transport — the BudgetItemFormModal pattern.
// What it pins: the exact VendorRequest body (every key, nulls explicit — PUT is a full
// replace), the live duplicate-name check (VendorNameRule, retired vendors included) and its
// open/restore link, server refusals shown verbatim, the "Show retired" toggle and search, the
// two-click retire, and a delete refused with Budgeting.Vendor.InUse offering to retire instead.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function vendor(id: string, name: string, isActive = true, over: Partial<VendorRecord> = {}): VendorRecord {
  return {
    id,
    name,
    contactName: null,
    email: null,
    phone: null,
    address: null,
    notes: null,
    gstRegistrationNumber: null,
    qboDisplayName: null,
    defaultBudgetCode: null,
    isActive,
    createdBy: null,
    modifiedBy: null,
    createdAtUtc: "2026-10-01T00:00:00+00:00",
    updatedAtUtc: "2026-10-01T00:00:00+00:00",
    ...over,
  };
}

const ACME = vendor("v1", "Acme Fuel", true, { contactName: "Dana Okemow", phone: "204-555-0142" });
const KAL = vendor("v2", "Kal Tire Thompson");
const OLD = vendor("v3", "Old Hardware Co", false);
const REGISTER = [ACME, KAL, OLD];

function makeApi(listResult: VendorRecord[] = REGISTER) {
  return {
    list: vi.fn<VendorsApi["list"]>().mockResolvedValue(listResult),
    create: vi.fn<VendorsApi["create"]>().mockResolvedValue("new-vendor"),
    update: vi.fn<VendorsApi["update"]>().mockResolvedValue(undefined),
    setActive: vi.fn<VendorsApi["setActive"]>().mockResolvedValue(undefined),
    remove: vi.fn<VendorsApi["remove"]>().mockResolvedValue(undefined),
    users: vi.fn<VendorsApi["users"]>().mockResolvedValue([]),
  };
}

// Queried by placeholder: ui/Field.tsx's FieldLabel passes no htmlFor, so labels don't associate.
const type = (placeholder: string, value: string) =>
  fireEvent.change(screen.getByPlaceholderText(placeholder), { target: { value } });

const NAME = "Kal Tire Thompson";
const EMAIL = "orders@example.com";
const CODE = "FLEET-MAINT";
const GST = "123456789 RT0001";
const CONTACT = "Dana Okemow";

function renderModal(props: Partial<Parameters<typeof VendorFormModal>[0]> = {}) {
  const onSaved = vi.fn();
  const onClose = vi.fn();
  const onOpenExisting = vi.fn();
  const api = makeApi();
  render(
    <VendorFormModal
      vendor={null}
      vendors={REGISTER}
      onClose={onClose}
      onSaved={onSaved}
      onOpenExisting={onOpenExisting}
      api={api}
      {...props}
    />,
  );
  return { api: (props.api as ReturnType<typeof makeApi>) ?? api, onSaved, onClose, onOpenExisting };
}

describe("VendorFormModal — create", () => {
  it("POSTs every VendorRequest key — trimmed, blanks null, code normalized — then hands back the refetched register", async () => {
    const created = vendor("new-vendor", "Esso Thompson", true, {
      email: "fuel@esso.example",
      defaultBudgetCode: "FLEET-FUEL",
      gstRegistrationNumber: "987654321 RT0001",
    });
    const api = makeApi([...REGISTER, created]);
    const { onSaved, onClose } = renderModal({ api });

    type(NAME, "  Esso Thompson ");
    type(EMAIL, " fuel@esso.example ");
    type(CODE, "fleet-fuel");
    type(GST, "987654321 RT0001");
    fireEvent.click(screen.getByText("CREATE VENDOR"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.create).toHaveBeenCalledWith({
      name: "Esso Thompson",
      contactName: null,
      email: "fuel@esso.example",
      phone: null,
      address: null,
      notes: null,
      gstRegistrationNumber: "987654321 RT0001",
      qboDisplayName: null,
      defaultBudgetCode: "FLEET-FUEL",
    });
    expect(onSaved).toHaveBeenCalledWith([...REGISTER, created], "new-vendor");
    expect(onClose).toHaveBeenCalled();
  });

  it("says the GST number is reference only, and what the QuickBooks name is for", () => {
    renderModal();
    expect(screen.getByText(/Reference only — the platform never calculates tax/)).toBeTruthy();
    expect(screen.getByText(/Used later to match imported QuickBooks expenses/)).toBeTruthy();
  });

  it("refuses a malformed email client-side with the server's own words", () => {
    const { api } = renderModal();
    type(NAME, "Esso Thompson");
    type(EMAIL, "204-555-0142");
    fireEvent.click(screen.getByText("CREATE VENDOR"));

    expect(screen.getByText("The email must look like an address (name@example.com).")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("shows a server 409 verbatim when the register it was given was stale", async () => {
    const message =
      'A vendor named "Esso Thompson" already exists. Vendor names are unique, ignoring case.';
    const api = makeApi();
    api.create.mockRejectedValueOnce(new ApiError("Budgeting.Vendor.DuplicateName", message, 409));
    const { onSaved } = renderModal({ api });

    type(NAME, "Esso Thompson");
    fireEvent.click(screen.getByText("CREATE VENDOR"));

    expect(await screen.findByText(message)).toBeTruthy();
    expect(onSaved).not.toHaveBeenCalled();
  });
});

describe("VendorFormModal — duplicate names", () => {
  it("warns live, ignoring case, refuses to POST, and offers to open the existing vendor", () => {
    const { api, onOpenExisting } = renderModal();

    type(NAME, "  acme FUEL ");
    const note = 'A vendor named "Acme Fuel" already exists. Vendor names are unique, ignoring case.';
    expect(screen.getByText(note)).toBeTruthy();
    expect(screen.getByText("Name taken")).toBeTruthy(); // the chip carries a written label

    fireEvent.click(screen.getByText("CREATE VENDOR"));
    expect(api.create).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText("OPEN ACME FUEL"));
    expect(onOpenExisting).toHaveBeenCalledWith(ACME);
  });

  it("catches a RETIRED vendor's name and offers to restore it instead", () => {
    const { onOpenExisting } = renderModal();

    type(NAME, "old hardware co");
    expect(
      screen.getByText(
        'A retired vendor named "Old Hardware Co" already exists. Reactivate it instead of adding it again.',
      ),
    ).toBeTruthy();

    fireEvent.click(screen.getByText("RESTORE OLD HARDWARE CO"));
    expect(onOpenExisting).toHaveBeenCalledWith(OLD);
  });
});

describe("VendorFormModal — edit", () => {
  it("PUTs every key to the vendor's id — a cleared field goes as null — and may re-case its own name", async () => {
    const edited = { ...ACME, name: "ACME FUEL", phone: null };
    const api = makeApi([edited, KAL, OLD]);
    const { onSaved } = renderModal({ vendor: ACME, api });

    expect(screen.getByDisplayValue(CONTACT)).toBeTruthy();
    type(NAME, "ACME FUEL"); // its own name, re-cased: not a duplicate (VendorNameRule's selfId)
    expect(screen.queryByText(/already exists/)).toBeNull();
    type("204-555-0142", "");
    fireEvent.click(screen.getByText("SAVE CHANGES"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith([edited, KAL, OLD], "v1"));
    expect(api.update).toHaveBeenCalledWith("v1", {
      name: "ACME FUEL",
      contactName: CONTACT,
      email: null,
      phone: null,
      address: null,
      notes: null,
      gstRegistrationNumber: null,
      qboDisplayName: null,
      defaultBudgetCode: null,
    });
    expect(api.create).not.toHaveBeenCalled();
  });

  it("still refuses a rename onto ANOTHER vendor's name", () => {
    const { api } = renderModal({ vendor: ACME });
    type(NAME, "kal tire thompson");
    fireEvent.click(screen.getByText("SAVE CHANGES"));
    expect(api.update).not.toHaveBeenCalled();
    expect(screen.getAllByText(/A vendor named "Kal Tire Thompson" already exists/).length).toBeGreaterThan(0);
  });
});

describe("Vendors screen", () => {
  async function renderScreen(api = makeApi()) {
    render(<Vendors api={api} />);
    await screen.findAllByText("Acme Fuel");
    return api;
  }

  it("asks for the whole register once and hides retired vendors until asked", async () => {
    const api = await renderScreen();
    expect(api.list).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Old Hardware Co")).toBeNull();

    fireEvent.click(screen.getByLabelText("Show retired (1)"));

    expect(screen.getByText("Old Hardware Co")).toBeTruthy();
    // Status is a chip with a written label, never colour alone.
    expect(screen.getAllByText("Retired").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Active").length).toBeGreaterThan(0);
  });

  it("filters by search across name and contact", async () => {
    await renderScreen();
    type("Name, contact, email, phone or code", "okemow");
    expect(screen.queryByText("Kal Tire Thompson")).toBeNull();
    expect(screen.getAllByText("Acme Fuel").length).toBeGreaterThan(0);
  });

  it("retires only on the second click, then refetches until the row reads retired", async () => {
    const api = await renderScreen();
    api.list.mockResolvedValue([{ ...ACME, isActive: false }, KAL, OLD]);

    fireEvent.click(screen.getByText("RETIRE"));
    expect(api.setActive).not.toHaveBeenCalled();
    expect(screen.getByText(/Retiring Acme Fuel keeps it listed/)).toBeTruthy();

    fireEvent.click(screen.getByText("CONFIRM RETIRE"));
    await waitFor(() => expect(api.setActive).toHaveBeenCalledWith("v1", false));
    await waitFor(() => expect(screen.getByText("RESTORE")).toBeTruthy());
  });

  it("shows a delete's InUse 409 verbatim and offers to retire instead", async () => {
    const message =
      "This vendor is referenced by budget items and cannot be deleted. Retire it instead — a retired vendor stays listed so existing items keep resolving.";
    const api = makeApi();
    api.remove.mockRejectedValueOnce(new ApiError("Budgeting.Vendor.InUse", message, 409));
    await renderScreen(api);

    fireEvent.click(screen.getByText("DELETE"));
    expect(api.remove).not.toHaveBeenCalled();
    fireEvent.click(screen.getByText("CONFIRM DELETE"));

    expect(await screen.findByText(message)).toBeTruthy();
    fireEvent.click(screen.getByText("RETIRE ACME FUEL INSTEAD"));
    expect(screen.getByText("CONFIRM RETIRE")).toBeTruthy();
    expect(api.setActive).not.toHaveBeenCalled();
  });

  it("from the modal's duplicate note, opens the retired vendor with its restore armed", async () => {
    await renderScreen();
    fireEvent.click(screen.getByText("+ NEW VENDOR"));
    type(NAME, "OLD HARDWARE CO");
    fireEvent.click(screen.getByText("RESTORE OLD HARDWARE CO"));

    expect(screen.queryByText("CREATE VENDOR")).toBeNull();
    expect(screen.getByText("CONFIRM RESTORE")).toBeTruthy();
    expect(screen.getByText(/Restoring Old Hardware Co/)).toBeTruthy();
  });

  it("shows a load failure verbatim with RETRY", async () => {
    const api = makeApi();
    api.list.mockRejectedValueOnce(
      new ApiError("Network.Unreachable", "The budgeting service is unreachable.", 0),
    );
    render(<Vendors api={api} />);

    expect(await screen.findByText("The budgeting service is unreachable.")).toBeTruthy();
    fireEvent.click(screen.getByText("RETRY"));
    await screen.findAllByText("Acme Fuel");
  });
});
