"use client";

import { getRole } from "@/lib/auth";
import { hasDriverAccess } from "@/lib/roles";
import AccessDeniedScreen from "@/components/AccessDeniedScreen";

// Sits inside AuthGate's signedIn branch, wrapping the Console. AuthGate answers "is there a
// session?"; this answers "may this session be here?".
//
// IMPORTANT — this is a UX gate, not a security boundary. The role comes from an unverified
// client-side decode of the access token (lib/claims.ts), so a determined user can get past it.
// The real boundary is the server-side DriverAccess policy in
// Backend/src/Api/NorthernLink.Api/Auth/AuthorizationPolicyRegistration.cs, attached to the
// driver-facing endpoint groups.
//
// And the boundary that matters MORE than the policy: a Driver token may read and write only
// its OWN driver row. DriverAccess admits dispatch staff too, so passing this gate says nothing
// about whose records you may touch — that check is server-side, on every {driverId} route, and
// nothing in this app may ever be written as though the client's driver id were trustworthy.
//
// getRole() reads a module-level variable rather than React state, so it is not reactive. That
// is fine: a role can only change with a new token, a new token means a login or a refresh, and
// both of those re-render through AuthGate. If that ever stops being true, lib/auth's
// onAuthChange is the hook to drive this from state instead.

export default function RoleGate({ children }: { children: React.ReactNode }) {
  const role = getRole();

  if (!hasDriverAccess(role)) {
    return <AccessDeniedScreen role={role} />;
  }

  return <>{children}</>;
}
