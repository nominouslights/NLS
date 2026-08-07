"use client";

import { useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import { currentPeriodId } from "@/lib/data";
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

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("periods");
  const [railCollapsed, setRailCollapsed] = useState(false);

  const [periodId, setPeriodId] = useState<string>(currentPeriodId);
  const [codeSel, setCodeSel] = useState<string | null>(null);

  function openCode(id: string | null) {
    setCodeSel(id);
    setScreen("codes");
  }

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
            <BudgetPeriods periodId={periodId} onSelectPeriod={setPeriodId} />
          )}
          {screen === "codes" && <BudgetCodes selId={codeSel} onSelect={setCodeSel} />}
          {screen === "allocations" && (
            <Allocations periodId={periodId} onSelectPeriod={setPeriodId} onOpenCode={openCode} />
          )}
          {screen === "actuals" && (
            <ActualsVsBudget periodId={periodId} onSelectPeriod={setPeriodId} />
          )}
          {screen === "variance" && (
            <Variance periodId={periodId} onSelectPeriod={setPeriodId} onOpenCode={openCode} />
          )}
          {screen === "reports" && <Reports periodId={periodId} />}
          {screen === "settings" && <Settings />}
        </div>
      </div>
    </div>
  );
}
