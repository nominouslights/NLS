import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'interceptors/auth_interceptor.dart';
import 'interceptors/logging_interceptor.dart';
import '../storage/secure_storage_service.dart';

Dio buildDioClient(SecureStorageService storageService) {
  final baseUrl = 'http://192.168.1.28:5046/api';

  final refreshDio = Dio(BaseOptions(baseUrl: baseUrl));

  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 15),
    headers: {'Content-Type': 'application/json'},
  ));

  dio.interceptors.add(AuthInterceptor(storageService, refreshDio));

  if (kDebugMode) {
    dio.interceptors.add(buildLoggingInterceptor());
  }

  return dio;
}
