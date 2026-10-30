// Mirrors Roles.DriverAccess in Backend/src/Shared/Kernel/Roles.cs — keep the two in step.
// The backend counterpart is registered as the "DriverAccess" authorization policy and unit
// tested in Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs; this list is
// tested in lib/roles.test.ts.
//
// The list is wider than "Driver" on purpose: dispatch staff must be able to see and act on
// everything a driver can, so Owner/Dispatcher/Supervisor are admitted too. The narrowing that
// actually matters is the other direction — a Driver token may read and write only its OWN
// driver row, and that is enforced server-side, never here.

export const DRIVER_ROLES = ["Owner", "Dispatcher", "Supervisor", "Driver"] as const;

export type DriverRole = (typeof DRIVER_ROLES)[number];

/**
 * Whether a role may use the Driver Field App. Case-sensitive, matching how the backend's
 * RequireRole compares — "driver" is not "Driver", and treating it as such here would let the
 * client show an app the API would then refuse to serve.
 */
export function hasDriverAccess(role: string | null | undefined): boolean {
  if (!role) return false;
  return (DRIVER_ROLES as readonly string[]).includes(role);
}
