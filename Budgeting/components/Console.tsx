"use client";

import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { colors } from "@/lib/theme";
import { isPeriodScoped, type ScreenId } from "@/lib/nav";
import type { BudgetPeriod } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import { listBudgetPeriods, toBudgetPeriod, type BudgetPeriodRecord } from "@/lib/api/budgeting";
import { getMyProfile, type MyProfile } from "@/lib/api/identity";
import { getClaims } from "@/lib/auth";
import {
  enteredPeriodStorageKey,
  localIsoDate,
  readEnteredPeriod,
  resolveEnteredPeriod,
  writeEnteredPeriod,
} from "@/lib/workingPeriod";
import { PeriodHoldContext } from "@/lib/periodHold";
import NavRail from "@/components/NavRail";
import TopBar from "@/components/TopBar";
import PeriodBanner from "@/components/PeriodBanner";
import BudgetPeriodFormModal from "@/components/BudgetPeriodFormModal";
import PeriodChooser from "@/components/screens/periods/PeriodChooser";
import BudgetPeriods from "@/components/screens/BudgetPeriods";
import BudgetCodes from "@/components/screens/BudgetCodes";
import ActualsVsBudget from "@/components/screens/ActualsVsBudget";
import Variance from "@/components/screens/Variance";
import Reports from "@/components/screens/Reports";
import Settings from "@/components/screens/Settings";

// The app shell, mirroring Dispatcher/components/Console.tsx: a 56px TopBar, a collapsible
// NavRail, and one screen rendered by plain && switching on a ScreenId. No routing — matching
// Dispatcher, where all screens live under a single route.
//
// The planner works INSIDE one period at a time ("enter a period"). Console holds only the
// entered id; the entered period itself is derived from the loaded list (resolveEnteredPeriod),
// so a refresh picks up its new totals and state with no bookkeeping. Exactly two handlers change
// it — enterPeriod (from the chooser, or creating a period) and switchPeriod (the banner's
// SWITCH PERIOD) — and no screen carries a period picker of its own. The id is remembered per
// tab and per user in sessionStorage (lib/workingPeriod.ts says why not localStorage).
//
// Period-scoped screens (lib/nav.ts PERIOD_SCOPED) render the chooser until a period is entered,
// and render inside a Fragment keyed by the period id, so every switch resets their state. Budget
// Codes and Settings are tenant-wide and render regardless; the banner says which case applies.
//
// A request against the entered period takes a hold (lib/periodHold.ts). While any hold is
// taken, SWITCH PERIOD and + NEW PERIOD refuse, so a result can never land on a period the
// planner has left. Rail navigation stays free — it never changes the period.
//
// Periods are fetched once on mount and threaded down as props so every screen agrees on the
// list. Budget codes and allocation lines are real too but are NOT hoisted: the screens that read
// them own their own fetches. Actuals and variance remain mock until their Stage 6.1 slice lands.
//
// The signed-in user's profile is hoisted for a different reason than periods: two places render
// it — Settings edits it, the TopBar shows it — and getClaims() cannot carry it. That is
// deliberate; the name is not in the token (it would be up to fifteen minutes stale, so saving
// would look broken) and lib/auth's onAuthChange is not the right channel either, since a
// profile is not auth state and gates nothing. Ordinary props from here, as periods already do.

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("periods");
  const [railCollapsed, setRailCollapsed] = useState(false);

  // null = still loading.
  const [periods, setPeriods] = useState<BudgetPeriod[] | null>(null);
  const [periodsError, setPeriodsError] = useState<{ message: string; code: string } | null>(null);
  // Console mounts only after AuthGate has signed in, client-side, so the claims and
  // sessionStorage are both readable in these initialisers. A sign-out unmounts Console, so a
  // different user signing in to the same tab gets a fresh key.
  const [storageKey] = useState(() => enteredPeriodStorageKey(getClaims()));
  /** The period being worked in; null = none entered. Changed only by enterPeriod/switchPeriod. */
  const [enteredId, setEnteredId] = useState<string | null>(() => readEnteredPeriod(storageKey));
  // Read once per mount; a chooser left open across midnight keeps yesterday's suggestion until
  // the next visit, which is harmless — it is only a suggestion.
  const [todayIso] = useState(() => localIsoDate());
  // codeSel survives the removal of the Allocations screen — Variance still jumps to a code.
  const [codeSel, setCodeSel] = useState<string | null>(null);
  /**
   * The New Period modal lives here, not in a screen: the TopBar pill and the chooser both open
   * it, from any screen, and creating a period enters it.
   */
  const [showCreate, setShowCreate] = useState(false);

  // The in-flight guard: a count of holds, provided to every screen (lib/periodHold.ts).
  const [holds, setHolds] = useState(0);
  const acquire = useCallback(() => {
    setHolds((n) => n + 1);
    let released = false;
    return () => {
      if (released) return;
      released = true;
      setHolds((n) => n - 1);
    };
  }, []);
  const hold = useMemo(() => ({ acquire }), [acquire]);
  const held = holds > 0;

  /** Replaces the list. Never touches the entered period — that is derived, not stored. */
  const applyLoaded = useCallback((records: BudgetPeriodRecord[]) => {
    setPeriods(records.map(toBudgetPeriod));
    setPeriodsError(null);
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

  const entered = resolveEnteredPeriod(periods, enteredId, periodsError !== null);
  const period = entered.period;
  const scoped = isPeriodScoped(screen);

  /** Enter a period. Stays on the current screen — the chooser was standing in for it. */
  function enterPeriod(id: string) {
    setEnteredId(id);
    writeEnteredPeriod(storageKey, id);
  }

  /** Leave the entered period. Refused while a request against it is still in flight. */
  function switchPeriod() {
    if (held) return;
    setEnteredId(null);
    writeEnteredPeriod(storageKey, null);
    if (!isPeriodScoped(screen)) setScreen("periods");
  }

  /** From the New Period modal: the fresh list already contains the new row — enter it. */
  function handlePeriodCreated(records: BudgetPeriodRecord[], id: string) {
    setPeriods(records.map(toBudgetPeriod));
    setPeriodsError(null);
    enterPeriod(id);
    setScreen("periods");
    // The modal calls onClose itself right after this, so the flag is cleared there.
  }

  /** The console's one create action — the TopBar pill and the chooser both open it. */
  function openNewPeriod() {
    if (held) return;
    setShowCreate(true);
  }

  function openCode(id: string | null) {
    setCodeSel(id);
    setScreen("codes");
  }

  const periodList = periods ?? [];

  return (
    <PeriodHoldContext.Provider value={hold}>
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
          onNewPeriod={openNewPeriod}
          newPeriodDisabled={held}
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
            <PeriodBanner
              period={period}
              scoped={scoped}
              held={held}
              onSwitch={switchPeriod}
              onChoose={() => setScreen("periods")}
            />
            <div style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
              {scoped && !period ? (
                <PeriodChooser
                  periods={periods}
                  error={periodsError}
                  lost={entered.lost}
                  returning={enteredId !== null}
                  todayIso={todayIso}
                  forScreen={screen}
                  onEnter={enterPeriod}
                  onCreate={openNewPeriod}
                  onRetry={loadPeriods}
                />
              ) : (
                // Keyed by the entered period, so every switch resets the screen's own state —
                // its confirms, modals and fetches can never outlive the period they were for.
                <Fragment key={period?.id ?? "none"}>
                  {screen === "periods" && period && (
                    <BudgetPeriods
                      period={period}
                      periods={periodList}
                      onPeriodsRefreshed={applyLoaded}
                    />
                  )}
                  {screen === "codes" && <BudgetCodes selId={codeSel} onSelect={setCodeSel} />}
                  {screen === "actuals" && period && <ActualsVsBudget period={period} />}
                  {screen === "variance" && period && (
                    <Variance period={period} onOpenCode={openCode} />
                  )}
                  {screen === "reports" && period && <Reports period={period} />}
                  {screen === "settings" && (
                    <Settings
                      profile={profile}
                      profileError={profileError}
                      onRetryProfile={loadProfile}
                      onProfileSaved={setProfile}
                    />
                  )}
                </Fragment>
              )}
            </div>
          </div>
        </div>
      </div>
      {showCreate && (
        <BudgetPeriodFormModal onClose={() => setShowCreate(false)} onSaved={handlePeriodCreated} />
      )}
    </PeriodHoldContext.Provider>
  );
}
