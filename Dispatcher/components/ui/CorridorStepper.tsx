import { fonts } from "@/lib/theme";

export function CorridorStepper({ stops }: { stops: string[] }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 0,
        padding: "14px 16px",
        background: "#0F1E33",
        border: "1px solid #1E3350",
        borderRadius: 10,
        marginBottom: 14,
        overflowX: "auto",
      }}
    >
      {stops.map((name, ix) => (
        <div key={`${name}-${ix}`} style={{ display: "flex", alignItems: "center", gap: 9, flex: "none" }}>
          {ix > 0 && <span style={{ color: "#3B5573", fontSize: 16, margin: "0 4px" }}>→</span>}
          <span
            style={{
              width: 9,
              height: 9,
              borderRadius: "50%",
              background: "#3B8DD4",
              flex: "none",
              boxShadow: "0 0 0 3px rgba(59,141,212,.18)",
            }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: "#c2d0e0", whiteSpace: "nowrap" }}>
            {name}
          </span>
        </div>
      ))}
    </div>
  );
}
