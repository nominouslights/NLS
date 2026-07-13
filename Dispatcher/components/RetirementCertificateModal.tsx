"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import {
  ApiError,
  formatKm,
  formatUtcDate,
  getRetirementCertificate,
  type RetirementCertificate,
} from "@/lib/api";
import { ActionButton } from "@/components/ui/Button";

// Printable retirement-certificate view (browser print in lieu of PDF — the
// certificate record on the backend is the durable artifact). The certificate
// body is deliberately rendered as a light "paper" document so it reads the
// same on screen and in print; @media print isolates it from the dark console.

const PRINT_CSS = `
@media print {
  body * { visibility: hidden; }
  .nl-cert-print, .nl-cert-print * { visibility: visible; }
  .nl-cert-print {
    position: fixed !important;
    inset: 0 !important;
    margin: 0 !important;
    box-shadow: none !important;
    border: none !important;
    border-radius: 0 !important;
    background: #ffffff !important;
  }
  .nl-cert-chrome { display: none !important; }
}
`;

function CertRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        gap: 16,
        padding: "9px 0",
        borderBottom: "1px solid #e2e2da",
      }}
    >
      <span
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 10.5,
          letterSpacing: ".12em",
          textTransform: "uppercase",
          color: "#6b6b60",
        }}
      >
        {label}
      </span>
      <span
        style={{
          fontFamily: mono ? fonts.mono : fonts.body,
          fontSize: mono ? 12.5 : 13,
          fontWeight: 600,
          color: "#22221e",
          textAlign: "right",
        }}
      >
        {value}
      </span>
    </div>
  );
}

export default function RetirementCertificateModal({
  vehicleId,
  onClose,
}: {
  vehicleId: string;
  onClose: () => void;
}) {
  const [cert, setCert] = useState<RetirementCertificate | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getRetirementCertificate(vehicleId)
      .then((c) => {
        if (!cancelled) setCert(c);
      })
      .catch((e) => {
        if (!cancelled) {
          setError(e instanceof ApiError ? e.message : "Failed to load certificate.");
        }
      });
    return () => {
      cancelled = true;
    };
  }, [vehicleId]);

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: "rgba(4,10,20,.7)",
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 26,
      }}
      className="detailfade"
    >
      <style dangerouslySetInnerHTML={{ __html: PRINT_CSS }} />
      <div
        style={{
          width: "100%",
          maxWidth: 640,
          maxHeight: "90vh",
          display: "flex",
          flexDirection: "column",
          gap: 12,
        }}
      >
        {/* certificate paper */}
        <div
          className="nl-cert-print"
          style={{
            background: "#faf9f4",
            borderRadius: 12,
            border: "1px solid #d8d8ce",
            boxShadow: "0 24px 64px rgba(0,0,0,.5)",
            overflowY: "auto",
            padding: "34px 38px",
          }}
        >
          {error ? (
            <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#a34400", fontWeight: 600 }}>
              ▲ {error}
            </div>
          ) : !cert ? (
            <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#6b6b60" }}>
              Loading certificate…
            </div>
          ) : (
            <div>
              <div style={{ textAlign: "center", marginBottom: 22 }}>
                <div
                  style={{
                    fontFamily: fonts.semiCondensed,
                    fontSize: 11,
                    letterSpacing: ".24em",
                    textTransform: "uppercase",
                    color: "#8a8a7c",
                    marginBottom: 6,
                  }}
                >
                  Northern Link Shuttle &amp; Cargo · Fleet &amp; Vehicle Management
                </div>
                <div
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 30,
                    lineHeight: 1.05,
                    color: "#1c1c18",
                  }}
                >
                  Certificate of Vehicle Retirement
                </div>
                <div
                  style={{
                    fontFamily: fonts.mono,
                    fontSize: 13,
                    color: "#4a4a42",
                    marginTop: 8,
                  }}
                >
                  {cert.certificateNumber}
                </div>
              </div>

              <div
                style={{
                  borderTop: "2px solid #22221e",
                  borderBottom: "1px solid #e2e2da",
                  marginBottom: 4,
                }}
              />

              <CertRow label="Unit number" value={cert.unitNumber} mono />
              <CertRow label="VIN" value={cert.vin} mono />
              <CertRow label="Vehicle" value={`${cert.year} ${cert.make} ${cert.model}`} />
              <CertRow label="Final odometer" value={formatKm(cert.finalOdometerKm)} mono />
              <CertRow label="Retirement reason" value={cert.retirementReason} />
              <CertRow label="Retired" value={formatUtcDate(cert.retiredAtUtc)} />
              <CertRow label="Certificate issued" value={formatUtcDate(cert.issuedAtUtc)} />

              <div
                style={{
                  marginTop: 22,
                  fontFamily: fonts.body,
                  fontSize: 11,
                  lineHeight: 1.6,
                  color: "#8a8a7c",
                }}
              >
                This certificate records the permanent retirement of the vehicle identified above
                from active service in the Northern Link fleet registry. The vehicle may
                subsequently be sold or recycled; it may not be returned to service. Certificate
                record ID {cert.id}.
              </div>
            </div>
          )}
        </div>

        {/* on-screen chrome — hidden in print */}
        <div className="nl-cert-chrome" style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
          <ActionButton onClick={onClose} style={{ background: colors.cardBg }}>
            CLOSE
          </ActionButton>
          {cert && (
            <ActionButton variant="primary" onClick={() => window.print()}>
              PRINT CERTIFICATE
            </ActionButton>
          )}
        </div>
      </div>
    </div>
  );
}
