import 'trip_post_report.dart';

class DelayEntry {
  final DateTime loggedAt;
  final IncidentType type;
  final String description;
  final int estimatedMinutes;

  const DelayEntry({
    required this.loggedAt,
    required this.type,
    required this.description,
    required this.estimatedMinutes,
  });
}

class DelayHandoff {
  final IncidentType type;
  final String description;

  const DelayHandoff({required this.type, required this.description});
}
