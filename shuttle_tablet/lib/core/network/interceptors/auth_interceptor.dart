import 'package:dio/dio.dart';
import '../../storage/secure_storage_service.dart';
import '../api_endpoints.dart';

class AuthInterceptor extends Interceptor {
  final SecureStorageService _storage;
  final Dio _refreshDio;

  AuthInterceptor(this._storage, this._refreshDio);

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    final token = await _storage.getAccessToken();
    if (token != null) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode == 401) {
      final refreshToken = await _storage.getRefreshToken();
      if (refreshToken == null) {
        await _storage.clearAll();
        handler.next(err);
        return;
      }
      try {
        final resp = await _refreshDio.post(
          ApiEndpoints.refresh,
          data: {'refreshToken': refreshToken},
        );
        final newAccessToken = resp.data['accessToken'] as String;
        await _storage.saveAccessToken(newAccessToken);

        final retryOptions = err.requestOptions;
        retryOptions.headers['Authorization'] = 'Bearer $newAccessToken';
        final retryResp = await _refreshDio.fetch(retryOptions);
        handler.resolve(retryResp);
      } catch (_) {
        await _storage.clearAll();
        handler.next(err);
      }
    } else {
      handler.next(err);
    }
  }
}
