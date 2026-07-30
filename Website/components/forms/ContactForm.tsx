"use client";

// PROTOTYPE FORM — no submission wired up. The backend owns the API contract and
// hasn't published contact endpoints yet, so this form never invents endpoint
// shapes and performs NO fetch. Client-side validation + in-place confirmation only.

import { useState } from "react";
import Field from "@/components/ui/Field";
import Button from "@/components/ui/Button";
import { bodyStyle, cardStyle, colors, fonts } from "@/lib/theme";

interface ContactValues {
  name: string;
  email: string;
  phone: string;
  topic: string;
  message: string;
}

const EMPTY: ContactValues = { name: "", email: "", phone: "", topic: "", message: "" };

const TOPIC_OPTIONS = [
  { value: "booking", label: "Booking or quote" },
  { value: "cargo", label: "Cargo & parcel" },
  { value: "medical", label: "NIHB medical travel" },
  { value: "corporate", label: "Corporate / crew contracts" },
  { value: "other", label: "Something else" },
];

function validate(v: ContactValues): Partial<Record<keyof ContactValues, string>> {
  const errors: Partial<Record<keyof ContactValues, string>> = {};
  if (!v.name.trim()) errors.name = "Please enter your name.";
  if (!v.email.trim()) errors.email = "Please enter an email so we can reply.";
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email.trim()))
    errors.email = "That email address doesn't look right.";
  if (!v.topic) errors.topic = "Please choose a topic.";
  if (!v.message.trim()) errors.message = "Please write a short message.";
  return errors;
}

export default function ContactForm() {
  const [values, setValues] = useState<ContactValues>(EMPTY);
  const [errors, setErrors] = useState<Partial<Record<keyof ContactValues, string>>>({});
  const [submitted, setSubmitted] = useState<ContactValues | null>(null);

  const set = (key: keyof ContactValues) => (value: string) =>
    setValues((prev) => ({ ...prev, [key]: value }));

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const errs = validate(values);
    setErrors(errs);
    if (Object.keys(errs).length === 0) setSubmitted(values);
  };

  if (submitted) {
    const topicLabel = TOPIC_OPTIONS.find((t) => t.value === submitted.topic)?.label ?? submitted.topic;
    const rows: [string, string][] = [
      ["Name", submitted.name],
      ["Email", submitted.email],
      ["Phone", submitted.phone || "—"],
      ["Topic", topicLabel],
      ["Message", submitted.message],
    ];
    return (
      <div
        className="fadein"
        role="status"
        style={{ ...cardStyle(28), borderColor: colors.teal, background: colors.tealTint }}
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
            Message received
          </h2>
        </div>
        <p style={bodyStyle(15.5)}>
          Thanks — here&apos;s what you wrote. (Prototype note: messages aren&apos;t
          transmitted yet — please call or email us directly for anything urgent.)
        </p>
        <dl style={{ margin: "18px 0 20px", display: "grid", gap: 8 }}>
          {rows.map(([label, value]) => (
            <div key={label} style={{ display: "flex", gap: 12 }}>
              <dt
                style={{
                  ...bodyStyle(14.5, colors.textMuted),
                  fontFamily: fonts.semiCondensed,
                  fontWeight: 600,
                  width: 90,
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
            setValues(EMPTY);
            setErrors({});
          }}
        >
          Send another message
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate style={cardStyle(28)}>
      <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
        <div className="nl-form-row">
          <Field label="Name" name="name" value={values.name} onChange={set("name")} required error={errors.name} placeholder="Your name" />
          <Field label="Email" name="email" value={values.email} onChange={set("email")} type="email" required error={errors.email} placeholder="you@example.com" />
        </div>
        <div className="nl-form-row">
          <Field label="Phone (optional)" name="phone" value={values.phone} onChange={set("phone")} type="tel" placeholder="(204) …" />
          <Field label="Topic" name="topic" value={values.topic} onChange={set("topic")} required options={TOPIC_OPTIONS} placeholder="Choose a topic…" error={errors.topic} />
        </div>
        <Field
          label="Message"
          name="message"
          value={values.message}
          onChange={set("message")}
          textarea
          required
          error={errors.message}
          placeholder="How can we help?"
        />
        <div>
          <Button type="submit" size="lg">
            Send Message
          </Button>
          <p style={{ ...bodyStyle(13.5, colors.textMuted), marginTop: 10 }}>
            Prototype — messages aren&apos;t transmitted yet. For anything urgent, call or
            email us directly.
          </p>
        </div>
      </div>
    </form>
  );
}
