import { request } from "./transport";

// Client for the signed-in user's own account — contract owned by Backend/ (Identity module,
// IdentityEndpoints.cs). A separate file from budgeting.ts, whose header binds it to the
// Budgeting module's routes, and from lib/auth.ts, which is a copied-and-adapted file concerned
// with token lifecycle rather than ordinary authenticated requests.
//
//   GET /api/identity/auth/profile  → MyProfile
//   PUT /api/identity/auth/profile  { fullName, jobTitle } → the stored MyProfile
//
// Both require authentication and no particular role: everyone owns a profile. The server reads
// the user id from the token's sub claim, so there is no id to pass and no way to name someone
// else's account.
//
// No refetchUntil here, unlike every budgeting write. That helper exists because budgeting reads
// come from projections that trail their writes; a profile reads the users table directly, and
// the PUT returns what was stored.

/** Mirrors MyProfileResponse. Email and role are read-only — shown, never sent. */
export interface MyProfile {
  userId: string;
  email: string;
  role: string;
  /** Null when the user has not set one, which is every account predating profiles. */
  fullName: string | null;
  jobTitle: string | null;
}

/** What a PUT sends. Null means "clear this field", never "leave it alone" — hence PUT. */
export interface MyProfileInput {
  fullName: string | null;
  jobTitle: string | null;
}

/**
 * Mirrors User.ProfileFieldMaxLength in Backend/src/Identity/Domain/Users/User.cs. Used to cap
 * the inputs so the server's length error is a backstop rather than the normal experience.
 */
export const PROFILE_FIELD_MAX_LENGTH = 128;

export function getMyProfile(): Promise<MyProfile> {
  return request<MyProfile>("/api/identity/auth/profile");
}

export function updateMyProfile(input: MyProfileInput): Promise<MyProfile> {
  return request<MyProfile>("/api/identity/auth/profile", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * How to render a user: their name when they have set one, their email otherwise. The single
 * place that fallback is decided for this app — mirrors what the server does for a budget code's
 * owner and audit columns, where a null name means "fall back to the email".
 */
export function profileDisplayName(profile: MyProfile): string {
  return profile.fullName?.trim() || profile.email;
}

/**
 * Trims and treats blank as absent, mirroring User.UpdateProfile's Normalize. Applied before a
 * PUT so the form sends the value the server would have stored anyway, and so the dirty check
 * agrees with the server's no-op rule (a stray space is not a change).
 */
export function normalizeProfileField(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
