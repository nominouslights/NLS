import 'dart:convert';
import 'dart:io';

// #region agent log
void agentLog({
  required String location,
  required String message,
  required String hypothesisId,
  Map<String, dynamic>? data,
  String runId = 'pre-fix',
}) {
  try {
    final payload = <String, dynamic>{
      'sessionId': 'c321d9',
      'runId': runId,
      'hypothesisId': hypothesisId,
      'location': location,
      'message': message,
      'data': data ?? <String, dynamic>{},
      'timestamp': DateTime.now().millisecondsSinceEpoch,
    };
    final paths = [
      r'c:\Users\Emelio Campbell\Documents\GitHub\Shuttle Software\debug-c321d9.log',
      'debug-c321d9.log',
    ];
    for (final path in paths) {
      try {
        File(path).writeAsStringSync(
          '${jsonEncode(payload)}\n',
          mode: FileMode.append,
          flush: true,
        );
        break;
      } catch (_) {}
    }
  } catch (_) {}
}
// #endregion
