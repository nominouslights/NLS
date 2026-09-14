// Mirrors Backend/src/Shared/Kernel/Roles.cs — keep the two in step. Same idea as
// Budgeting/lib/roles.ts (which mirrors only BudgetAccess); this one carries both lists because
// the launcher gates every app tile client-side.
//
// These lists are a UX gate only: they decide which tiles on the Home screen are enabled. The
// real boundary is the matching authorization policy on each API endpoint — a role that gets
// past this list and calls an endpoint it may not use gets the server's 403, and the screen's
// existing error surfaces it.

/** Roles.DispatchAccess — dispatch-capable internal staff. */
export const DISPATCH_ROLES = ["Owner", "Dispatcher", "Supervisor"] as const;

/** Roles.BudgetAccess — financial oversight. */
export const BUDGET_ROLES = ["Owner", "Accountant"] as const;

/**
 * Roles.LegacyAdmin — the literal every user created before the role model carries. It always
 * meant Owner in practice, and the backend's AdminOnly policy still honours it for the tokens
 * minted before the rename migration ran, so the launcher treats it as Owner too.
 */
export const LEGACY_ADMIN_ROLE = "Admin";

/** "Admin" → "Owner"; every other value passes through unchanged (null/undefined → null). */
export function normalizeRole(role: string | null | undefined): string | null {
  if (!role) return null;
  return role === LEGACY_ADMIN_ROLE ? "Owner" : role;
}

/**
 * Whether `role` is in `allowed`. Case-sensitive, matching how the backend's RequireRole
 * compares — "owner" is not "Owner", and treating it as such here would enable a tile the API
 * would then refuse to serve.
 */
export function hasRole(allowed: readonly string[], role: string | null | undefined): boolean {
  if (!role) return false;
  return allowed.includes(role);
}
