import { colors, fonts } from "@/lib/theme";
import { ActionButton } from "./Button";

/**
 * Page footer for a server-paged list: "1–50 of 312" with ‹ Prev / Next ›.
 * Renders nothing when everything already fits on one page.
 *
 * `page` is 1-based. ActionButton is a span whose `disabled` only drops the click
 * handler, so the ends of the range are also dimmed to read as unavailable.
 */
export function Pager({
  page,
  pageSize,
  totalCount,
  onPage,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  onPage: (next: number) => void;
}) {
  const lastPage = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount <= pageSize) return null;

  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, totalCount);
  const atStart = page <= 1;
  const atEnd = page >= lastPage;

  return (
    <div
      style={{
        flex: "none",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 10,
        padding: "10px 16px",
        borderTop: `1px solid ${colors.borderSubtle}`,
        background: colors.cardBg,
      }}
    >
      <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textMuted }}>
        {first}–{last} of {totalCount}
      </span>
      <span style={{ display: "flex", gap: 8 }}>
        <ActionButton
          variant="secondary"
          disabled={atStart}
          onClick={() => onPage(page - 1)}
          style={{ fontSize: 12, padding: "5px 12px" }}
        >
          ‹ PREV
        </ActionButton>
        <ActionButton
          variant="secondary"
          disabled={atEnd}
          onClick={() => onPage(page + 1)}
          style={{ fontSize: 12, padding: "5px 12px" }}
        >
          NEXT ›
        </ActionButton>
      </span>
    </div>
  );
}
