// Mirrors Roles.BudgetAccess in Backend/src/Shared/Kernel/Roles.cs — keep the two in step.
// The backend counterpart is registered as the "BudgetAccess" authorization policy and unit
// tested in Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs; this list is
// tested in lib/roles.test.ts. Both exist because in Stage 6.0 there are no budgeting
// endpoints yet, so this list is the gate a user actually meets.

export const BUDGET_ROLES = ["Owner", "Accountant"] as const;

export type BudgetRole = (typeof BUDGET_ROLES)[number];

/**
 * Whether a role may use the Budgeting console. Case-sensitive, matching how the backend's
 * RequireRole compares — "owner" is not "Owner", and treating it as such here would let the
 * client show a console the API would then refuse to serve.
 */
export function hasBudgetAccess(role: string | null | undefined): boolean {
  if (!role) return false;
  return (BUDGET_ROLES as readonly string[]).includes(role);
}
