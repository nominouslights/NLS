"use client";

import type { AppProps } from "@/components/apps/shell";
import Bookings from "@/components/screens/Bookings";
import BookingPolicy from "@/components/screens/BookingPolicy";

// Community Booking — its own app because community booking follows its own rules
// (demand-activated departures, a tenant-wide policy). Nothing to retain: the calendar and the
// policy panel each own their fetch.
export default function CommunityBookingApp({ screen, shell }: AppProps) {
  return (
    <>
      {screen === "bookings" && <Bookings onOpenTrip={shell.openTrip} />}
      {screen === "bookingPolicy" && <BookingPolicy />}
    </>
  );
}
