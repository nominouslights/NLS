"use client";

import { getRole } from "@/lib/auth";
import { hasBudgetAccess } from "@/lib/roles";
import AccessDeniedScreen from "@/components/AccessDeniedScreen";

// Sits inside AuthGate's signedIn branch, wrapping the Console. AuthGate answers "is there a
// session?"; this answers "may this session be here?".
//
// IMPORTANT — this is a UX gate, not a security boundary. The role comes from an unverified
// client-side decode of the access token (lib/claims.ts), so a determined user can get past it.
// That is acceptable today because there is nothing behind it but mock data: Stage 6.1's
// /api/budgeting/* endpoints carry the server-side BudgetAccess policy, which is the real
// boundary. Do not add anything to this app that treats passing this gate as proof of anything.
//
// getRole() reads a module-level variable rather than React state, so it is not reactive. That
// is fine: a role can only change with a new token, a new token means a login or a refresh, and
// both of those re-render through AuthGate. If that ever stops being true, lib/auth's
// onAuthChange is the hook to drive this from state instead.

export default function RoleGate({ children }: { children: React.ReactNode }) {
  const role = getRole();

  if (!hasBudgetAccess(role)) {
    return <AccessDeniedScreen role={role} />;
  }

  return <>{children}</>;
}
