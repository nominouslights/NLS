export interface Service {
  slug: string;
  name: string;
  glyph: string; // simple text glyph — no icon libraries on this site
  short: string; // one-liner for cards
  overview: string[]; // paragraphs for the detail page
  features: string[];
  audience: string;
}

export interface FleetVehicle {
  id: string;
  name: string;
  kind: "coach" | "van";
  seats: number;
  blurb: string;
  features: string[];
}

export interface Stat {
  value: string;
  label: string;
}

export interface Faq {
  group: "Booking" | "Routes" | "Cargo" | "Medical Travel";
  q: string;
  a: string;
}

export interface Testimonial {
  quote: string;
  name: string;
  role: string;
}
