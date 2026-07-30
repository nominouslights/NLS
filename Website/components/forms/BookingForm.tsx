"use client";

// PROTOTYPE FORM — no submission wired up. The backend owns the API contract and
// hasn't published booking endpoints yet, so this form never invents endpoint
// shapes and performs NO fetch. When the contract exists, wire it via the /api/*
// proxy (see next.config.ts). Until then: client-side validation + an in-place
// confirmation panel.

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import Field from "@/components/ui/Field";
import Button from "@/components/ui/Button";
import { COMMUNITIES, SERVICES } from "@/lib/data";
import { bodyStyle, cardStyle, colors, fonts } from "@/lib/theme";

interface BookingValues {
  name: string;
  phone: string;
  email: string;
  service: string;
  pickup: string;
  destination: string;
  date: string;
  passengers: string;
  notes: string;
}

const EMPTY: BookingValues = {
  name: "",
  phone: "",
  email: "",
  service: "",
  pickup: "",
  destination: "",
  date: "",
  passengers: "1",
  notes: "",
};

const SERVICE_OPTIONS = SERVICES.map((s) => ({ value: s.slug, label: s.name }));
const COMMUNITY_OPTIONS = [
  ...COMMUNITIES.map((c) => ({ value: c, label: c })),
  { value: "Other", label: "Other / not listed" },
];

function validate(v: BookingValues): Partial<Record<keyof BookingValues, string>> {
  const errors: Partial<Record<keyof BookingValues, string>> = {};
  if (!v.name.trim()) errors.name = "Please enter your name.";
  if (!v.phone.trim()) errors.phone = "Please enter a phone number so we can confirm your trip.";
  if (v.email.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email.trim()))
    errors.email = "That email address doesn't look right.";
  if (!v.service) errors.service = "Please choose a service.";
  if (!v.pickup) errors.pickup = "Please choose a pickup community.";
  if (!v.destination) errors.destination = "Please choose a destination.";
  if (!v.date) errors.date = "Please pick a travel date.";
  return errors;
}

export default function BookingForm() {
  const searchParams = useSearchParams();
  const requested = searchParams.get("service") ?? "";
  const prefill = SERVICES.some((s) => s.slug === requested) ? requested : "";

  const [values, setValues] = useState<BookingValues>({ ...EMPTY, service: prefill });
  const [errors, setErrors] = useState<Partial<Record<keyof BookingValues, string>>>({});
  const [submitted, setSubmitted] = useState<BookingValues | null>(null);

  const set = (key: keyof BookingValues) => (value: string) =>
    setValues((prev) => ({ ...prev, [key]: value }));

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const errs = validate(values);
    setErrors(errs);
    if (Object.keys(errs).length === 0) setSubmitted(values);
  };

  if (submitted) {
    const serviceName =
      SERVICES.find((s) => s.slug === submitted.service)?.name ?? submitted.service;
    const rows: [string, string][] = [
      ["Name", submitted.name],
      ["Phone", submitted.phone],
      ["Email", submitted.email || "—"],
      ["Service", serviceName],
      ["Trip", `${submitted.pickup} → ${submitted.destination}`],
      ["Date", submitted.date],
      ["Passengers", submitted.passengers],
      ["Notes", submitted.notes || "—"],
    ];
    return (
      <div
        className="fadein"
        role="status"
        style={{
          ...cardStyle(28),
          borderColor: colors.teal,
          background: colors.tealTint,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
          <span aria-hidden="true" style={{ color: colors.tealDark, fontSize: 24, fontWeight: 700 }}>
            ✓
          </span>
          <h2
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 26,
              textTransform: "uppercase",
              color: colors.ink,
              margin: 0,
            }}
          >
            Request received
          </h2>
        </div>
        <p style={bodyStyle(15.5)}>
          Thanks, {submitted.name.split(" ")[0]} — here&apos;s what you sent us. We&apos;ll
          confirm your seat and time by phone or email. (Prototype note: online requests
          aren&apos;t transmitted yet — please call or email to book today.)
        </p>
        <dl style={{ margin: "18px 0 20px", display: "grid", gap: 8 }}>
          {rows.map(([label, value]) => (
            <div key={label} style={{ display: "flex", gap: 12 }}>
              <dt
                style={{
                  ...bodyStyle(14.5, colors.textMuted),
                  fontFamily: fonts.semiCondensed,
                  fontWeight: 600,
                  width: 110,
                  flex: "none",
                }}
              >
                {label}
              </dt>
              <dd style={{ ...bodyStyle(14.5, colors.ink), margin: 0 }}>{value}</dd>
            </div>
          ))}
        </dl>
        <Button
          variant="secondary"
          onClick={() => {
            setSubmitted(null);
            setValues({ ...EMPTY, service: prefill });
            setErrors({});
          }}
        >
          Send another request
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate style={cardStyle(28)}>
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <div className="nl-form-row">
          <Field label="Full name" name="name" value={values.name} onChange={set("name")} required error={errors.name} placeholder="Your name" />
          <Field label="Phone" name="phone" value={values.phone} onChange={set("phone")} type="tel" required error={errors.phone} placeholder="(204) …" />
        </div>
        <div className="nl-form-row">
          <Field label="Email (optional)" name="email" value={values.email} onChange={set("email")} type="email" error={errors.email} placeholder="you@example.com" />
          <Field label="Service" name="service" value={values.service} onChange={set("service")} required options={SERVICE_OPTIONS} placeholder="Choose a service…" error={errors.service} />
        </div>
        <div className="nl-form-row">
          <Field label="Pickup community" name="pickup" value={values.pickup} onChange={set("pickup")} required options={COMMUNITY_OPTIONS} placeholder="Choose pickup…" error={errors.pickup} />
          <Field label="Destination" name="destination" value={values.destination} onChange={set("destination")} required options={COMMUNITY_OPTIONS} placeholder="Choose destination…" error={errors.destination} />
        </div>
        <div className="nl-form-row">
          <Field label="Travel date" name="date" value={values.date} onChange={set("date")} type="date" required error={errors.date} />
          <Field label="Passengers" name="passengers" value={values.passengers} onChange={set("passengers")} type="number" />
        </div>
        <Field
          label="Notes (optional)"
          name="notes"
          value={values.notes}
          onChange={set("notes")}
          textarea
          placeholder="Cargo details, NIHB voucher info, Gift-a-Seat recipient, accessibility needs…"
        />
        <div>
          <Button type="submit" size="lg">
            Request Booking
          </Button>
          <p style={{ ...bodyStyle(13.5, colors.textMuted), marginTop: 10 }}>
            Prototype — requests aren&apos;t transmitted yet. To book today, call or email us
            directly.
          </p>
        </div>
      </div>
    </form>
  );
}
