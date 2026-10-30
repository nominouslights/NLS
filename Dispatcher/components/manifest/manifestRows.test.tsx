import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import type { ManifestPassenger } from "@/lib/api/trips";
import {
  bookeoBookingNumber,
  emptyPax,
  PassengerRowsEditor,
  paxRowsFromManifest,
  paxRowsToWire,
  type StopOption,
} from "./manifestRows";

// `externalRef` ("bookeo:<booking number>") is how a Bookeo re-import recognises
// the manifest rows it owns. The editor must carry it from load to save
// UNCHANGED — including when the dispatcher edits that passenger's name, where
// the backend's name-based carry-over could not find it — and a row a person
// adds must go up with null.

afterEach(cleanup);

const STOPS: StopOption[] = [
  { stopId: "s-lynn", name: "Lynn Lake" },
  { stopId: "s-thompson", name: "Thompson" },
];

const passenger = (over: Partial<ManifestPassenger>): ManifestPassenger => ({
  name: "Jane Doe",
  email: null,
  phone: null,
  pickupStopId: "s-lynn",
  pickupStopName: "Lynn Lake",
  dropoffStopId: "s-thompson",
  dropoffStopName: "Thompson",
  idVerified: false,
  boardedOn: false,
  boardedOff: false,
  fareAmountCad: null,
  farePaymentMethod: null,
  farePaidAtUtc: null,
  externalRef: null,
  ...over,
});

describe("manifest externalRef round-trip", () => {
  it("carries an imported row's ref through load → edit → save, even across a rename", () => {
    const loaded = [
      passenger({ name: "Jane Doe", externalRef: "bookeo:1234567" }),
      passenger({ name: "Walk-up Rider", externalRef: null }),
    ];

    const rows = paxRowsFromManifest(loaded, STOPS);
    expect(rows.map((r) => r.externalRef)).toEqual(["bookeo:1234567", null]);

    // The dispatcher corrects the imported passenger's name and changes a stop.
    const edited = rows.map((r, i) => (i === 0 ? { ...r, name: "Janet Doe-Smith", dropoffIdx: "0" } : r));
    const wire = paxRowsToWire(edited, STOPS, 8);

    expect(wire[0]).toMatchObject({ name: "Janet Doe-Smith", dropoffStopName: "Lynn Lake", externalRef: "bookeo:1234567" });
    expect(wire[1]).toMatchObject({ name: "Walk-up Rider", externalRef: null });
  });

  it("sends the ref back byte-for-byte (no trimming or re-casing)", () => {
    const ref = "bookeo:AB-0099 ";
    const wire = paxRowsToWire(paxRowsFromManifest([passenger({ externalRef: ref })], STOPS), STOPS, 8);
    expect(wire[0].externalRef).toBe(ref);
  });

  it("treats a missing ref on an older response as null", () => {
    const legacy = passenger({});
    delete (legacy as Partial<ManifestPassenger>).externalRef;
    expect(paxRowsFromManifest([legacy], STOPS)[0].externalRef).toBeNull();
  });

  it("a newly added row sends externalRef: null", () => {
    const loaded = paxRowsFromManifest([passenger({ externalRef: "bookeo:1234567" })], STOPS);
    const withNew = [...loaded, { ...emptyPax(), name: "Added By Hand" }];
    const wire = paxRowsToWire(withNew, STOPS, 8);
    expect(wire.map((p) => p.externalRef)).toEqual(["bookeo:1234567", null]);
    expect(Object.prototype.hasOwnProperty.call(wire[1], "externalRef")).toBe(true);
  });

  it("serializes externalRef into the request body for every row", () => {
    const wire = paxRowsToWire([{ ...emptyPax(), name: "New Person" }], STOPS, 8);
    expect(JSON.parse(JSON.stringify(wire))[0].externalRef).toBeNull();
  });
});

describe("Bookeo row marker", () => {
  it("parses the booking number only from a bookeo: ref", () => {
    expect(bookeoBookingNumber("bookeo:1234567")).toBe("1234567");
    expect(bookeoBookingNumber(null)).toBeNull();
    expect(bookeoBookingNumber("other:1")).toBeNull();
  });

  it("labels imported rows with text, and leaves manual rows unmarked", () => {
    const rows = paxRowsFromManifest(
      [passenger({ name: "Jane Doe", externalRef: "bookeo:1234567" }), passenger({ name: "Walk-up Rider" })],
      STOPS,
    );
    render(<PassengerRowsEditor rows={rows} stops={STOPS} onChange={() => {}} />);

    const markers = screen.getAllByTestId("bookeo-row-marker");
    expect(markers).toHaveLength(1);
    expect(markers[0].textContent).toContain("BOOKEO #1234567");
    expect(markers[0].textContent).toMatch(/re-import may update/);
  });
});
