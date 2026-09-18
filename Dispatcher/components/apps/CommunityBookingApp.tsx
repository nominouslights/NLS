"use client";

import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import Bookings from "@/components/screens/Bookings";
import BookingPolicy from "@/components/screens/BookingPolicy";

// Community Booking — its own app because community booking follows its own rules
// (demand-activated departures, a tenant-wide policy). The calendar and the policy panel each
// own their fetch; the one thing retained is the opened booking (the detail view is selection
// state inside the Bookings screen, same pattern as Fleet's fleet.selId), so a detour Home and
// back lands on the same booking.
export default function CommunityBookingApp({ screen, shell }: AppProps) {
  const [bookingSelId, setBookingSelId] = useRetainedState<string | null>("communityBooking.bookingSelId", null);

  return (
    <>
      {screen === "bookings" && (
        <Bookings onOpenTrip={shell.openTrip} bookingSelId={bookingSelId} setBookingSelId={setBookingSelId} />
      )}
      {screen === "bookingPolicy" && <BookingPolicy />}
    </>
  );
}
