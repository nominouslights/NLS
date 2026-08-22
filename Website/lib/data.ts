import type { Faq, FleetVehicle, Service, Stat, Testimonial } from "@/lib/types";

// ALL site copy lives here. Every claim below is grounded in real business facts
// (Thompson MB hub; the five communities; the Thompson↔Lynn Lake corridor; the
// 24-seat International coaches and 7-seat Ford Transit T-150 vans; Class 4
// drivers and NSC compliance; the Alamos Gold crew contract; Gift-a-Seat;
// voucher-based NIHB). Do not add ridership numbers, fares, or schedules —
// those don't exist as published facts yet.

export const CONTACT = {
  phone: "(204) 555-0100", // PLACEHOLDER — swap before launch
  email: "info@northernlinkshuttle.ca", // PLACEHOLDER — swap before launch
  address: "Thompson, Manitoba", // depot address TBD — swap before launch
  hours: "Call or email — we respond during regular business hours.",
} as const;

export const COMMUNITIES = [
  "Thompson",
  "Leaf Rapids",
  "Lynn Lake",
  "South Indian Lake",
  "Black Sturgeon Falls",
] as const;

export const SERVICES: Service[] = [
  {
    slug: "corporate-crew-shuttle",
    name: "Corporate & Crew Shuttle",
    glyph: "▲",
    short:
      "Scheduled crew transportation for industrial clients along the Thompson–Lynn Lake corridor, including our crew contract with Alamos Gold.",
    overview: [
      "Northern Manitoba's industrial operations depend on crews arriving rested, on time, and together. Our corporate shuttle service runs scheduled crew transportation along the Thompson–Lynn Lake corridor in 24-seat International coaches, driven by Class 4 licensed professional drivers.",
      "We currently operate a crew shuttle contract for Alamos Gold's Lynn Lake operations — the same corridor discipline, vehicle standards, and compliance practices are available to any employer moving people in the region.",
    ],
    features: [
      "Scheduled crew runs on the Thompson ↔ Lynn Lake corridor",
      "24-seat International coaches sized for full crews",
      "Class 4 licensed, professionally trained drivers",
      "NSC-compliant operation: daily vehicle inspections (DVIR) and hours-of-service tracking",
      "Contract terms shaped around your rotation schedule",
    ],
    audience:
      "Mining, construction, and industrial employers moving crews between Thompson, Lynn Lake, and points along the corridor.",
  },
  {
    slug: "community-shuttle",
    name: "Community Shuttle",
    glyph: "●",
    short:
      "Scheduled passenger service connecting Thompson, Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls — with the Gift-a-Seat program.",
    overview: [
      "The community shuttle is the backbone of what we do: scheduled passenger service linking Thompson, Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls. Whether it's appointments, family visits, work, or errands in Thompson, the shuttle keeps northern communities connected year-round.",
      "Through our Gift-a-Seat program, you can purchase a seat for someone else — a way for families, friends, and community organizations to make sure a ride is never the reason someone stays home.",
    ],
    features: [
      "Serves all five communities: Thompson, Leaf Rapids, Lynn Lake, South Indian Lake, Black Sturgeon Falls",
      "Gift-a-Seat: buy a seat for a family member or community member in need",
      "24-seat coaches on main runs, 7-seat vans where they fit better",
      "Class 4 licensed drivers who know these roads in every season",
    ],
    audience:
      "Residents of the five communities travelling for appointments, work, shopping, and family.",
  },
  {
    slug: "nihb-medical-transport",
    name: "NIHB Medical Transport",
    glyph: "✚",
    short:
      "Voucher-based medical travel under the Non-Insured Health Benefits program — getting patients to appointments reliably and with dignity.",
    overview: [
      "Medical appointments shouldn't depend on finding a ride. We provide voucher-based transportation under the Non-Insured Health Benefits (NIHB) program, carrying patients between their home communities and medical services.",
      "Our drivers are Class 4 licensed professionals operating NSC-compliant vehicles, and our scheduled corridor service means medical travel connects communities like Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls with Thompson dependably.",
    ],
    features: [
      "Voucher-based travel under the NIHB program",
      "Service between the five communities and Thompson",
      "Class 4 licensed drivers, NSC-compliant vehicles",
      "Coach and 7-seat van options depending on the trip",
    ],
    audience:
      "NIHB-eligible patients and the health service coordinators who arrange their travel.",
  },
  {
    slug: "charter-service",
    name: "Charter Service",
    glyph: "★",
    short:
      "Private charters across Northern Manitoba — 24-seat coaches or 7-seat vans for teams, groups, and events.",
    overview: [
      "Need a vehicle and a professional driver for your own itinerary? Our charter service puts a 24-seat International coach or a 7-seat Ford Transit T-150 van at your disposal, with a Class 4 licensed driver behind the wheel.",
      "Sports teams, community groups, work parties, events — if it involves moving people in Northern Manitoba, tell us where and when and we'll quote the trip.",
    ],
    features: [
      "24-seat coaches for full groups, 7-seat vans for smaller parties",
      "Your route, your schedule — one-way, return, or multi-stop",
      "Class 4 licensed professional drivers",
      "NSC-compliant fleet with daily inspections",
    ],
    audience:
      "Teams, schools, community organizations, and employers with one-off or recurring group travel.",
  },
  {
    slug: "cargo-parcel",
    name: "Cargo & Parcel",
    glyph: "▪",
    short:
      "Parcels and cargo moved along our scheduled routes between Thompson and the communities we serve.",
    overview: [
      "Every scheduled run is also a freight opportunity. Our cargo and parcel service moves packages between Thompson and Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls on vehicles already making the trip.",
      "Drop off at our Thompson depot or arrange a pickup point in your community — parts, documents, supplies, and personal parcels ride the corridor with us.",
    ],
    features: [
      "Parcel service along the Thompson ↔ Lynn Lake corridor",
      "Thompson depot drop-off",
      "Same professional, NSC-compliant operation as our passenger service",
      "Combine with the Weekly Grocery Run for household essentials",
    ],
    audience:
      "Businesses, band offices, and households sending parcels between Thompson and the communities.",
  },
  {
    slug: "weekly-grocery-run",
    name: "Weekly Grocery Run",
    glyph: "◗",
    short:
      "A weekly scheduled run dedicated to groceries and household essentials between Thompson and outlying communities.",
    overview: [
      "Groceries in remote communities are expensive and choices are thin. The Weekly Grocery Run is a scheduled service dedicated to moving groceries and household essentials between Thompson — with its full-size stores — and the outlying communities we serve.",
      "Contact us for this week's schedule and how to get your community or household on the run.",
    ],
    features: [
      "Weekly scheduled service — dependable, not ad hoc",
      "Connects outlying communities with Thompson's grocery stores",
      "Runs on our NSC-compliant fleet with professional drivers",
      "Pairs with our Cargo & Parcel service for other essentials",
    ],
    audience:
      "Households and community organizations in Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls.",
  },
];

export const FLEET: FleetVehicle[] = [
  {
    id: "international-coach",
    name: "International Coach",
    kind: "coach",
    seats: 24,
    blurb:
      "Our workhorse for crew shuttles and community runs: a 24-seat International coach built for long corridor miles in northern conditions.",
    features: [
      "24 passenger seats",
      "Corridor runs: Thompson ↔ Lynn Lake and community service",
      "Daily vehicle inspection reports (DVIR)",
      "Operated by Class 4 licensed drivers",
    ],
  },
  {
    id: "ford-transit-t150",
    name: "Ford Transit T-150 Van",
    kind: "van",
    seats: 7,
    blurb:
      "A 7-seat Ford Transit T-150 for smaller groups, NIHB medical trips, parcels, and runs where a full coach is more than the job needs.",
    features: [
      "7 passenger seats",
      "Suited to NIHB medical transport and small charters",
      "Flexible for parcel and grocery runs",
      "Same NSC-compliant inspection and hours-of-service regime as the coaches",
    ],
  },
];

// Honesty guard: only verifiable facts — no invented ridership or trip counts.
export const STATS: Stat[] = [
  { value: "5", label: "Communities served" },
  { value: "6", label: "Service lines" },
  { value: "24", label: "Seats per coach" },
  { value: "Class 4", label: "Licensed professional drivers" },
];

export const FAQS: Faq[] = [
  {
    group: "Booking",
    q: "How do I book a seat?",
    a: "Use the booking form on this site to send us a request, or call us directly. We'll confirm your seat, pickup point, and time by phone or email. Online booking with instant confirmation is coming later.",
  },
  {
    group: "Booking",
    q: "What is Gift-a-Seat?",
    a: "Gift-a-Seat lets you purchase a community shuttle seat for someone else — a family member, an Elder, anyone who needs the ride. Tell us who the seat is for when you book and we'll take care of the rest.",
  },
  {
    group: "Booking",
    q: "How much does a trip cost?",
    a: "Fares depend on the route and service. Contact us by phone or email for current fares and charter quotes — we'll give you a straight answer before you commit to anything.",
  },
  {
    group: "Routes",
    q: "Which communities do you serve?",
    a: "Thompson, Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls, with Thompson as our hub and depot. Our main corridor runs Thompson ↔ Lynn Lake.",
  },
  {
    group: "Routes",
    q: "When do shuttles run?",
    a: "Schedules vary by service and season. Contact us for the current schedule on your route — the Weekly Grocery Run in particular runs on a fixed weekly day we'll confirm when you get in touch.",
  },
  {
    group: "Routes",
    q: "Do you run in winter?",
    a: "Yes — this is Northern Manitoba, and winter is when dependable service matters most. Our drivers are Class 4 licensed professionals, our vehicles get daily inspections, and we make conservative weather calls when conditions demand it.",
  },
  {
    group: "Cargo",
    q: "Can I send a parcel without travelling myself?",
    a: "Yes. Parcels and cargo ride our scheduled runs between Thompson and the communities we serve. Drop off at our Thompson depot or arrange a pickup point in your community.",
  },
  {
    group: "Cargo",
    q: "How does the Weekly Grocery Run work?",
    a: "It's a scheduled weekly service dedicated to groceries and household essentials between Thompson and the outlying communities. Contact us for this week's schedule and how to get your household or community on the run.",
  },
  {
    group: "Medical Travel",
    q: "Do you accept NIHB vouchers?",
    a: "Yes. Our NIHB Medical Transport service is voucher-based under the Non-Insured Health Benefits program. Book with your voucher details and we'll handle the trip.",
  },
  {
    group: "Medical Travel",
    q: "Can a health coordinator book on a patient's behalf?",
    a: "Absolutely — coordinators arrange much of our medical travel. Call or email us with the patient's community, appointment time, and voucher details.",
  },
];

// PLACEHOLDER testimonials — replace with real, permissioned quotes before launch.
// These are sample layouts only and are labeled as such in the UI.
export const TESTIMONIALS: Testimonial[] = [
  {
    quote:
      "Placeholder quote — a community rider's words about depending on the shuttle will go here.",
    name: "Community Rider",
    role: "Sample — real quote coming soon",
  },
  {
    quote:
      "Placeholder quote — a corporate client's words about crew transportation reliability will go here.",
    name: "Corporate Client",
    role: "Sample — real quote coming soon",
  },
  {
    quote:
      "Placeholder quote — a medical travel coordinator's words about NIHB trips will go here.",
    name: "Travel Coordinator",
    role: "Sample — real quote coming soon",
  },
];

export const COMPLIANCE_POINTS = [
  {
    title: "Class 4 Drivers",
    body: "Every passenger run is driven by a Class 4 licensed professional driver.",
  },
  {
    title: "NSC Compliant",
    body: "We operate under the National Safety Code standards that govern commercial carriers.",
  },
  {
    title: "Daily Inspections",
    body: "Every vehicle gets a daily vehicle inspection report (DVIR) before it carries a passenger or parcel.",
  },
  {
    title: "Hours of Service",
    body: "Driver hours are tracked and managed so no one is behind the wheel fatigued.",
  },
] as const;
