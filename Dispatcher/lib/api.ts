// ---------------------------------------------------------------------------
// Barrel — preserves the original single-file `lib/api` surface after the
// split into lib/api/*. Existing imports from "@/lib/api" keep working
// unchanged; new code may import from the domain modules directly:
//   ./api/transport   — API_BASE, ApiError, request, requestBlob, identity*
//   ./api/format      — formatCad, formatKm, formatUtcDate (cross-domain)
//   ./api/fleet       — vehicles: types, endpoints, display helpers
//   ./api/maintenance — inspections, shops, documents, service records, work orders
//   ./api/pm          — preventative maintenance: plans, due status, completions
//   ./api/trips       — trips/routes/templates AND the trip-manifest contract
// PM display helpers live outside the barrel in ../pmDisplay — import them
// directly, exactly like ../workOrderDisplay.
// The trip-manifest symbols are re-exported by NAME below (no blanket
// `export * from "./api/trips"` — trips.ts carries additional symbols that
// were never part of lib/api's surface and could collide).
// ---------------------------------------------------------------------------

export * from "./api/transport";
export * from "./api/format";
export * from "./api/fleet";
export * from "./api/maintenance";
export * from "./api/pm";

export type {
  ManifestSource,
  ManifestDirection,
  ManifestCargoSecured,
  ManifestPassenger,
  ManifestCargo,
  TripManifest,
  TripManifestInput,
  TripActivityEntry,
} from "./api/trips";
export {
  createTripManifest,
  updateTripManifest,
  listTripManifests,
  getTripManifest,
  listTripActivity,
} from "./api/trips";

export type {
  CustomerRecord,
  CustomerInput,
  CorridorRecord,
  CalendarDaySummary,
  BookingDayStatus,
  BookingStatus,
  BookingPaymentMethod,
  BookingPaymentStatus,
  BookingLocation,
  BookingLocationInput,
  BookingPassengerRecord,
  BookingPassengerInput,
  BookingRecord,
  DayDetail,
  BookingInput,
  BookingUpdateInput,
  BookingPolicy,
  BookingPolicyInput,
  CorridorSettingsRecord,
  BookingSettings,
} from "./api/booking";
export {
  searchCustomers,
  createCustomer,
  getCustomer,
  updateCustomer,
  listCorridors,
  getCalendarMonth,
  getDayDetail,
  createBooking,
  updateBooking,
  confirmBooking,
  cancelBooking,
  guaranteeDay,
  getBookingSettings,
  updateBookingPolicy,
  upsertCorridorSettings,
  setDayOverrides,
  bookingStatusKind,
  dayStatusKind,
  locationLabel,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
} from "./api/booking";

export type {
  NotificationServiceType,
  EmailDispatchStatus,
  EmailRecipientStatus,
  EmailTemplateRecord,
  EmailTemplateInput,
  EmailTemplateListParams,
  EmailPreviewInput,
  EmailPreviewResult,
  SendRecipientInput,
  SendTripPickupEmailInput,
  EmailRecipientResult,
  EmailDispatchRecord,
} from "./api/notifications";
export {
  listEmailTemplates,
  getEmailTemplate,
  createEmailTemplate,
  updateEmailTemplate,
  setEmailTemplateActive,
  previewEmailTemplate,
  sendTripPickupEmail,
  listTripEmailDispatches,
  MERGE_FIELDS,
  NOTIFICATION_SERVICE_TYPE_LABELS,
  svcForNotificationServiceType,
  isEmailContact,
  dispatchChip,
} from "./api/notifications";
