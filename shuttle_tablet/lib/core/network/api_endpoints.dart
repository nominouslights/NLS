class ApiEndpoints {
  static const String auth = '/auth';
  static const String login = '/auth/login';
  static const String register = '/auth/register';
  static const String refresh = '/auth/refresh';

  static const String trips = '/trips';
  static String tripById(String id) => '/trips/$id';
  static String tripAssignDriver(String id) => '/trips/$id/assign-driver';
  static String tripDispatch(String id) => '/trips/$id/dispatch';
  static String tripStatus(String id) => '/trips/$id/status';
  static String tripPreInspection(String id) => '/trips/$id/pre-inspection';
  static String tripPostReport(String id) => '/trips/$id/post-report';

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

  static const String auditEvents = '/audit-events';

  static const String clients = '/clients';
  static String clientById(String id) => '/clients/$id';
  static String contractsByClient(String clientId) => '/clients/$clientId/contracts';
  static String contractById(String clientId, String contractId) => '/clients/$clientId/contracts/$contractId';
  static String contractRates(String clientId, String contractId) => '/clients/$clientId/contracts/$contractId/rates';
  static String deleteRateLine(String clientId, String rateLineId) => '/clients/$clientId/rates/$rateLineId';
  static String rateLinesByClient(String clientId) => '/clients/$clientId/rate-lines';

  static const String pendingUsers = '/users/pending';
  static String approveUser(String id) => '/users/$id/approve';
  static String rejectUser(String id) => '/users/$id/reject';

  static const String locations = '/locations';
  static String locationById(String id) => '/locations/$id';
}
