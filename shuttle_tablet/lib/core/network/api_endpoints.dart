class ApiEndpoints {
  static const String auth = '/auth';
  static const String login = '/auth/login';
  static const String register = '/auth/register';
  static const String refresh = '/auth/refresh';
  static const String changePassword = '/auth/change-password';

  static const String setupStatus = '/setup/status';
  static const String setupInitialize = '/setup/initialize';

  static const String trips = '/trips';
  static String tripById(String id) => '/trips/$id';
  static String tripAssignDriver(String id) => '/trips/$id/assign-driver';
  static String tripDispatch(String id) => '/trips/$id/dispatch';
  static String tripStatus(String id) => '/trips/$id/status';
  static String tripPreInspection(String id) => '/trips/$id/pre-inspection';
  static String tripPostReport(String id) => '/trips/$id/post-report';
  static String tripPassengers(String id) => '/trips/$id/passengers';
  static String tripPassengerById(String id, String pid) => '/trips/$id/passengers/$pid';
  static String tripPassengerPaymentStatus(String id, String pid) =>
      '/trips/$id/passengers/$pid/payment-status';
  static String tripPassengerBoardingStatus(String id, String pid) =>
      '/trips/$id/passengers/$pid/boarding';
  static String tripPassengerSendConfirmation(String id, String pid) =>
      '/trips/$id/passengers/$pid/send-confirmation';
  static String tripSendStopUpdate(String id) => '/trips/$id/send-stop-update';
  static String tripCargo(String id) => '/trips/$id/cargo';
  static String tripCargoById(String id, String cid) => '/trips/$id/cargo/$cid';

  static const String drivers = '/drivers';
  static String driverById(String id) => '/drivers/$id';
  static String driverStatus(String id) => '/drivers/$id/status';
  static String driverDocuments(String id) => '/drivers/$id/documents';
  static String driverDocumentById(String dId, String docId) => '/drivers/$dId/documents/$docId';
  static String driverDocumentDownload(String dId, String docId) => '/drivers/$dId/documents/$docId/download';
  static String driverRoster(String id) => '/drivers/$id/roster';
  static String driverRosterEntry(String dId, String eId) => '/drivers/$dId/roster/$eId';
  static const String fleetRoster = '/drivers/roster';

  static const String passengers = '/passengers';
  static String passengerById(String id) => '/passengers/$id';

  static const String vehicles = '/vehicles';
  static String vehicleById(String id) => '/vehicles/$id';
  static String vehicleStatus(String id) => '/vehicles/$id/status';
  static String vehicleOutOfService(String id) => '/vehicles/$id/out-of-service';
  static String vehicleOdometer(String id) => '/vehicles/$id/odometer';
  static String vehicleServiceRecords(String id) => '/vehicles/$id/service-records';
  static String vehicleServiceRecordById(String vId, String rId) =>
      '/vehicles/$vId/service-records/$rId';
  static String vehicleServiceRecordComplete(String vId, String rId) =>
      '/vehicles/$vId/service-records/$rId/complete';
  static String vehicleInspectionRecords(String id) => '/vehicles/$id/inspection-records';
  static String vehicleInspectionRecordById(String vId, String rId) =>
      '/vehicles/$vId/inspection-records/$rId';
  static String vehicleFuelEntries(String id) => '/vehicles/$id/fuel-entries';
  static String vehicleFuelEntryById(String vId, String eId) =>
      '/vehicles/$vId/fuel-entries/$eId';
  static String vehicleOdometerHistory(String id) =>
      '/vehicles/$id/odometer-history';

  static const String communityCalendar = '/community/calendar';
  static const String communityAdminCalendar = '/community/calendar/admin';
  static const String communityBookings = '/community/bookings';
  static String bookingByRef(String r) => '/community/bookings/$r';
  static const String communityBlocks = '/community/calendar/blocks';
  static String blockByDate(String d) => '/community/calendar/blocks/$d';

  static const String auditEvents = '/audit-events';

  static const String clients = '/clients';
  static String clientById(String id) => '/clients/$id';
  static String contractsByClient(String clientId) => '/clients/$clientId/contracts';
  static String contractById(String clientId, String contractId) => '/clients/$clientId/contracts/$contractId';
  static String contractRates(String clientId, String contractId) => '/clients/$clientId/contracts/$contractId/rates';
  static String deleteRateLine(String clientId, String rateLineId) => '/clients/$clientId/rates/$rateLineId';
  static String rateLinesByClient(String clientId) => '/clients/$clientId/rate-lines';
  static String clientEmailTemplates(String clientId) => '/clients/$clientId/email-templates';
  static String clientEmailTemplateByType(String clientId, String type) =>
      '/clients/$clientId/email-templates/$type';
  static String purchaseOrdersByClient(String clientId) => '/clients/$clientId/purchase-orders';
  static String purchaseOrderById(String clientId, String id) =>
      '/clients/$clientId/purchase-orders/$id';

  static const String pendingUsers = '/users/pending';
  static String approveUser(String id) => '/users/$id/approve';
  static String rejectUser(String id) => '/users/$id/reject';

  static const String locations = '/locations';
  static String locationById(String id) => '/locations/$id';
}
