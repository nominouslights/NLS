import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import '../auth/auth_event_bus.dart';
import 'interceptors/auth_interceptor.dart';
import 'interceptors/logging_interceptor.dart';
import '../storage/secure_storage_service.dart';

Dio buildDioClient(SecureStorageService storageService, AuthEventBus authEventBus) {
  final baseUrl = dotenv.env['API_BASE_URL']!;

  final refreshDio = Dio(BaseOptions(baseUrl: baseUrl));

  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 15),
    headers: {'Content-Type': 'application/json'},
  ));

  dio.interceptors.add(AuthInterceptor(storageService, refreshDio, authEventBus));

  if (kDebugMode) {
    dio.interceptors.add(buildLoggingInterceptor());
  }

  return dio;
}
