import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import ProfileForm from "@/components/ProfileForm";
import { ApiError } from "@/lib/api/transport";
import type { MyProfile } from "@/lib/api/identity";

// The form takes onSave as a prop, so this drives it with a plain vi.fn() rather than stubbing
// the transport — keeping lib/api/budgeting.test.ts's rule that no client test mocks request().

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function profile(over: Partial<MyProfile> = {}): MyProfile {
  return {
    userId: "8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70",
    email: "planner@northernlink.ca",
    role: "Accountant",
    fullName: null,
    jobTitle: null,
    ...over,
  };
}

// Queried by placeholder: ui/Field.tsx's FieldLabel renders "{children} {hint}" in one <label>
// and passes no htmlFor, so neither the label text nor a form-control association is matchable.
const NAME_PLACEHOLDER = "e.g. Léa Fontaine";
const TITLE_PLACEHOLDER = "e.g. Financial Planner";

function input(placeholder: string): HTMLInputElement {
  return screen.getByPlaceholderText(placeholder) as HTMLInputElement;
}

function saveButton(): HTMLElement {
  return screen.getByText(/SAVE PROFILE|SAVING…/);
}

describe("ProfileForm", () => {
  it("seeds its inputs from the profile", () => {
    render(
      <ProfileForm
        profile={profile({ fullName: "Léa Fontaine", jobTitle: "Financial Planner" })}
        onSave={vi.fn()}
        onSaved={vi.fn()}
      />,
    );

    expect(input(NAME_PLACEHOLDER).value).toBe("Léa Fontaine");
    expect(input(TITLE_PLACEHOLDER).value).toBe("Financial Planner");
  });

  it("shows an account with no profile as empty, not as the word null", () => {
    render(<ProfileForm profile={profile()} onSave={vi.fn()} onSaved={vi.fn()} />);

    expect(input(NAME_PLACEHOLDER).value).toBe("");
    expect(input(TITLE_PLACEHOLDER).value).toBe("");
  });

  it("renders the email and role as read-only facts", () => {
    // Email is the sign-in identifier and role is an owner's decision — neither is editable
    // here, so neither gets an input.
    render(<ProfileForm profile={profile()} onSave={vi.fn()} onSaved={vi.fn()} />);

    expect(screen.getByText("planner@northernlink.ca")).toBeTruthy();
    expect(screen.getByText("Accountant")).toBeTruthy();
    expect(screen.queryByDisplayValue("planner@northernlink.ca")).toBeNull();
  });

  it("disables SAVE until something actually changes", () => {
    render(
      <ProfileForm profile={profile({ fullName: "Léa Fontaine" })} onSave={vi.fn()} onSaved={vi.fn()} />,
    );

    expect(saveButton().getAttribute("aria-disabled")).toBe("true");

    fireEvent.change(input(NAME_PLACEHOLDER), { target: { value: "Léa Fontaine-Roy" } });

    expect(saveButton().getAttribute("aria-disabled")).toBeNull();
  });

  it("treats a stray space as no change, matching the server's no-op rule", () => {
    render(
      <ProfileForm profile={profile({ fullName: "Léa Fontaine" })} onSave={vi.fn()} onSaved={vi.fn()} />,
    );

    fireEvent.change(input(NAME_PLACEHOLDER), { target: { value: "  Léa Fontaine  " } });

    expect(saveButton().getAttribute("aria-disabled")).toBe("true");
  });

  it("sends trimmed values, and null for a cleared field", async () => {
    const onSave = vi.fn(async () => profile({ fullName: "Léa Fontaine" }));
    render(
      <ProfileForm
        profile={profile({ fullName: "Lea", jobTitle: "Planner" })}
        onSave={onSave}
        onSaved={vi.fn()}
      />,
    );

    fireEvent.change(input(NAME_PLACEHOLDER), { target: { value: "  Léa Fontaine  " } });
    fireEvent.change(input(TITLE_PLACEHOLDER), { target: { value: "   " } });
    fireEvent.click(saveButton());

    await waitFor(() =>
      expect(onSave).toHaveBeenCalledWith({ fullName: "Léa Fontaine", jobTitle: null }),
    );
  });

  it("hands the saved profile back so the rest of the console can follow", async () => {
    const saved = profile({ fullName: "Léa Fontaine" });
    const onSaved = vi.fn();
    render(
      <ProfileForm profile={profile()} onSave={vi.fn(async () => saved)} onSaved={onSaved} />,
    );

    fireEvent.change(input(NAME_PLACEHOLDER), { target: { value: "Léa Fontaine" } });
    fireEvent.click(saveButton());

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(saved));
    expect(screen.getByText(/Saved\./)).toBeTruthy();
  });

  it("surfaces the server's message verbatim when a save is refused", async () => {
    const onSave = vi.fn(async () => {
      throw new ApiError(
        "Identity.User.FullNameTooLong",
        "A full name may be at most 128 characters.",
        400,
      );
    });
    render(<ProfileForm profile={profile()} onSave={onSave} onSaved={vi.fn()} />);

    fireEvent.change(input(NAME_PLACEHOLDER), { target: { value: "Léa Fontaine" } });
    fireEvent.click(saveButton());

    await waitFor(() =>
      expect(screen.getByText("A full name may be at most 128 characters.")).toBeTruthy(),
    );
    expect(screen.getByText("Identity.User.FullNameTooLong")).toBeTruthy();
    // Still editable and still dirty — the user has to be able to correct and retry.
    expect(saveButton().getAttribute("aria-disabled")).toBeNull();
  });
});
