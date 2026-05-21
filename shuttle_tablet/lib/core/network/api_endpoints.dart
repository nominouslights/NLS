class ApiEndpoints {
  static const String auth = '/auth';
  static const String login = '/auth/login';
  static const String register = '/auth/register';
  static const String refresh = '/auth/refresh';

  static const String trips = '/trips';
  static String tripById(String id) => '/trips/$id';

  static const String drivers = '/drivers';
  static String driverById(String id) => '/drivers/$id';

  static const String passengers = '/passengers';
  static String passengerById(String id) => '/passengers/$id';

  static const String vehicles = '/vehicles';
  static String vehicleById(String id) => '/vehicles/$id';
  static String vehicleLocation(String id) => '/vehicles/$id/location';

  static const String auditEvents = '/audit-events';

  static const String pendingUsers = '/users/pending';
  static String approveUser(String id) => '/users/$id/approve';
  static String rejectUser(String id) => '/users/$id/reject';
}
