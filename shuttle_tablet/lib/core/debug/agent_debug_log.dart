import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';

/// Debug-session logging for agent investigation (session 4fbad0).
void agentDebugLog({
  required String location,
  required String message,
  required String hypothesisId,
  Map<String, dynamic>? data,
  String runId = 'pre-fix',
}) {
  final payload = <String, dynamic>{
    'sessionId': '4fbad0',
    'runId': runId,
    'hypothesisId': hypothesisId,
    'location': location,
    'message': message,
    'data': data ?? <String, dynamic>{},
    'timestamp': DateTime.now().millisecondsSinceEpoch,
  };
  final line = jsonEncode(payload);

  // #region agent log
  debugPrint('AGENT_LOG:$line');
  _postToIngest(line);
  // #endregion
}

Future<void> _postToIngest(String line) async {
  const hosts = ['127.0.0.1', '10.0.2.2'];
  for (final host in hosts) {
    try {
      final client = HttpClient();
      final request = await client.postUrl(
        Uri.parse('http://$host:7415/ingest/c6fd324e-18bf-4ed5-b1e9-88e1908512d0'),
      );
      request.headers.set('Content-Type', 'application/json');
      request.headers.set('X-Debug-Session-Id', '4fbad0');
      request.write(line);
      await request.close();
      client.close();
      return;
    } catch (_) {
      continue;
    }
  }
}
