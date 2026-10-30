// Why this file exists.
//
// `<input type="time">` accepts exactly one value format: 24-hour "HH:MM". Anything else — and
// "6:30 AM" is anything else — is rejected by the browser and the field renders EMPTY. There is
// no error, no console warning, nothing; the time simply is not there.
//
// So seeding a time input with `hhmm()` (the 12-hour DISPLAY formatter) made editing a schedule
// template look like it had never had a departure time. Worse than cosmetic: the blank box was
// then submitted back, writing an empty time over a real one. The same mistake was in the trip
// window editor and the return-leg form.
//
// These pin the two formatters apart. If someone "simplifies" them back into one, this fails.

import { describe, expect, it } from "vitest";
import { hhmm, timeInputValue } from "./trips";

/** What a `<input type="time">` will actually render. Anything else shows blank. */
const TIME_INPUT_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

describe("timeInputValue", () => {
  it("turns the API's TimeOnly into something a time input can render", () => {
    // The API sends TimeOnly as "HH:MM:SS" — see TripScheduleTemplate.departureTime.
    expect(timeInputValue("06:30:00")).toBe("06:30");
    expect(timeInputValue("14:05:00")).toBe("14:05");
    expect(timeInputValue("00:00:00")).toBe("00:00");
    expect(timeInputValue("23:59:00")).toBe("23:59");
  });

  it("is idempotent, so a value already read back out of an input survives a round trip", () => {
    expect(timeInputValue("06:30")).toBe("06:30");
    expect(timeInputValue(timeInputValue("06:30:00"))).toBe("06:30");
  });

  it("zero-pads, because '6:30' is as blank to the browser as '6:30 AM'", () => {
    expect(timeInputValue("6:30")).toBe("06:30");
    expect(timeInputValue("6:5")).toBe("06:05");
  });

  it("returns an empty string for nothing, which is what an empty input holds", () => {
    // Deliberately NOT hhmm's "—": an em dash in a time input is another silent blank.
    expect(timeInputValue(null)).toBe("");
    expect(timeInputValue(undefined)).toBe("");
    expect(timeInputValue("")).toBe("");
    expect(timeInputValue("not a time")).toBe("");
  });

  it("produces a value the browser will actually render, for every hour of the day", () => {
    for (let hour = 0; hour < 24; hour += 1) {
      const api = `${String(hour).padStart(2, "0")}:45:00`;
      expect(timeInputValue(api)).toMatch(TIME_INPUT_PATTERN);
    }
  });
});

describe("hhmm is for display and must never seed a time input", () => {
  it("returns a 12-hour string the browser cannot render", () => {
    expect(hhmm("06:30:00")).toBe("6:30 AM");
    expect(hhmm("14:05:00")).toBe("2:05 PM");

    // The bug, stated as an assertion: this is why the field went blank.
    expect(hhmm("06:30:00")).not.toMatch(TIME_INPUT_PATTERN);
    expect(hhmm("14:05:00")).not.toMatch(TIME_INPUT_PATTERN);
  });

  it("renders midnight and noon as a reader expects, unlike the input format", () => {
    expect(hhmm("00:15:00")).toBe("12:15 AM");
    expect(hhmm("12:00:00")).toBe("12:00 PM");
  });

  it("uses an em dash for nothing, which is right on a page and wrong in an input", () => {
    expect(hhmm(null)).toBe("—");
    expect(timeInputValue(null)).toBe("");
  });
});
