"use client";

import { useCallback, useEffect, useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import type { BudgetPeriod } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import { listBudgetPeriods, toBudgetPeriod, type BudgetPeriodRecord } from "@/lib/api/budgeting";
import { getMyProfile, type MyProfile } from "@/lib/api/identity";
import NavRail from "@/components/NavRail";
import TopBar from "@/components/TopBar";
import BudgetPeriods from "@/components/screens/BudgetPeriods";
import BudgetCodes from "@/components/screens/BudgetCodes";
import Allocations from "@/components/screens/Allocations";
import ActualsVsBudget from "@/components/screens/ActualsVsBudget";
import Variance from "@/components/screens/Variance";
import Reports from "@/components/screens/Reports";
import Settings from "@/components/screens/Settings";

// The app shell, mirroring Dispatcher/components/Console.tsx: a 56px TopBar, a collapsible
// NavRail, and one screen rendered by plain && switching on a ScreenId. No routing — matching
// Dispatcher, where all screens live under a single route.
//
// Selection and period state is hoisted here rather than kept inside the screens, following the
// same reasoning as Dispatcher: switching screens unmounts them, and someone who has stepped
// back two periods should not lose that by glancing at Budget Codes.
//
// Periods are the app's first real data (GET /api/budgeting/periods) and live here too — fetched
// once on mount, threaded down as props so every screen agrees on the list. Budget codes and
// allocation lines are real too but are NOT hoisted: the screens that read them own their own
// fetches. Actuals and variance remain mock until their Stage 6.1 slice lands.
//
// The signed-in user's profile is hoisted for a different reason than periods: two places render
// it — Settings edits it, the TopBar shows it — and getClaims() cannot carry it. That is
// deliberate; the name is not in the token (it would be up to fifteen minutes stale, so saving
// would look broken) and lib/auth's onAuthChange is not the right channel either, since a
// profile is not auth state and gates nothing. Ordinary props from here, as periods already do.

/** The period to open on: the one containing today, else the latest, else none. */
function defaultPeriodId(periods: BudgetPeriod[]): string {
  if (periods.length === 0) return "";
  const today = new Date();
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(
    today.getDate(),
  ).padStart(2, "0")}`;
  const current = periods.find((p) => p.startsOn <= iso && iso <= p.endsOn);
  if (current) return current.id;
  return periods.reduce((a, b) => (a.startsOn >= b.startsOn ? a : b)).id;
}

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("periods");
  const [railCollapsed, setRailCollapsed] = useState(false);

  // null = still loading. periodId stays "" until the first load picks one.
  const [periods, setPeriods] = useState<BudgetPeriod[] | null>(null);
  const [periodsError, setPeriodsError] = useState<{ message: string; code: string } | null>(null);
  const [periodId, setPeriodId] = useState<string>("");
  const [codeSel, setCodeSel] = useState<string | null>(null);

  const applyLoaded = useCallback((records: BudgetPeriodRecord[]) => {
    const rows = records.map(toBudgetPeriod);
    setPeriods(rows);
    setPeriodsError(null);
    setPeriodId((prev) => (prev && rows.some((p) => p.id === prev) ? prev : defaultPeriodId(rows)));
  }, []);

  const applyLoadError = useCallback((e: unknown) => {
    setPeriods((prev) => prev ?? []);
    setPeriodsError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Failed to load budget periods.", code: "Unknown" },
    );
  }, []);

  /** Retry handler — the mount fetch below uses then-callbacks per the Stops.tsx lint idiom. */
  const loadPeriods = useCallback(() => {
    listBudgetPeriods().then(applyLoaded, applyLoadError);
  }, [applyLoaded, applyLoadError]);

  useEffect(() => {
    let active = true;
    listBudgetPeriods().then(
      (records) => {
        if (active) applyLoaded(records);
      },
      (e) => {
        if (active) applyLoadError(e);
      },
    );
    return () => {
      active = false;
    };
  }, [applyLoaded, applyLoadError]);

  // null = still loading. The signed-in user's own account.
  const [profile, setProfile] = useState<MyProfile | null>(null);
  const [profileError, setProfileError] = useState<{ message: string; code: string } | null>(null);

  const applyProfileError = useCallback((e: unknown) => {
    setProfileError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Failed to load your profile.", code: "Unknown" },
    );
  }, []);

  const applyProfile = useCallback((loaded: MyProfile) => {
    setProfile(loaded);
    setProfileError(null);
  }, []);

  /** Retry handler, mirroring loadPeriods. */
  const loadProfile = useCallback(() => {
    getMyProfile().then(applyProfile, applyProfileError);
  }, [applyProfile, applyProfileError]);

  useEffect(() => {
    let active = true;
    getMyProfile().then(
      (loaded) => {
        if (active) applyProfile(loaded);
      },
      (e) => {
        if (active) applyProfileError(e);
      },
    );
    return () => {
      active = false;
    };
  }, [applyProfile, applyProfileError]);

  /** From the New Period modal: the fresh list already contains the new row — select it. */
  function handlePeriodCreated(records: BudgetPeriodRecord[], id: string) {
    setPeriods(records.map(toBudgetPeriod));
    setPeriodId(id);
  }

  function openCode(id: string | null) {
    setCodeSel(id);
    setScreen("codes");
  }

  const periodList = periods ?? [];

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100vh",
        width: "100%",
        background: colors.pageBg,
        overflow: "hidden",
      }}
    >
      <TopBar
        onToggleRail={() => setRailCollapsed((v) => !v)}
        onNewAllocation={() => setScreen("allocations")}
        fullName={profile?.fullName ?? null}
      />
      <div style={{ display: "flex", flex: 1, minHeight: 0 }}>
        <NavRail screen={screen} collapsed={railCollapsed} onSelect={setScreen} />
        <div
          style={{
            flex: 1,
            minWidth: 0,
            background: colors.mainBg,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}
        >
          {screen === "periods" && (
            <BudgetPeriods
              periods={periods}
              error={periodsError}
              onRetry={loadPeriods}
              periodId={periodId}
              onSelectPeriod={setPeriodId}
              onCreated={handlePeriodCreated}
              onPeriodsRefreshed={applyLoaded}
            />
          )}
          {screen === "codes" && <BudgetCodes selId={codeSel} onSelect={setCodeSel} />}
          {screen === "allocations" && (
            <Allocations
              periods={periodList}
              periodId={periodId}
              onSelectPeriod={setPeriodId}
              onOpenCode={openCode}
            />
          )}
          {screen === "actuals" && (
            <ActualsVsBudget periods={periodList} periodId={periodId} onSelectPeriod={setPeriodId} />
          )}
          {screen === "variance" && (
            <Variance
              periods={periodList}
              periodId={periodId}
              onSelectPeriod={setPeriodId}
              onOpenCode={openCode}
            />
          )}
          {screen === "reports" && <Reports periods={periodList} periodId={periodId} />}
          {screen === "settings" && (
            <Settings
              profile={profile}
              profileError={profileError}
              onRetryProfile={loadProfile}
              onProfileSaved={setProfile}
            />
          )}
        </div>
      </div>
    </div>
  );
}
