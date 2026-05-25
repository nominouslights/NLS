import 'package:dio/dio.dart';
import '../../auth/auth_event_bus.dart';
import '../../storage/secure_storage_service.dart';
import '../api_endpoints.dart';

class AuthInterceptor extends Interceptor {
  final SecureStorageService _storage;
  final Dio _refreshDio;
  final AuthEventBus _authEventBus;

  AuthInterceptor(this._storage, this._refreshDio, this._authEventBus);

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
        await _forceLogout(handler, err);
        return;
      }
      try {
        final resp = await _refreshDio.post(
          ApiEndpoints.refresh,
          data: {'refreshToken': refreshToken},
        );
        final newAccessToken = resp.data['accessToken'] as String;
        final newRefreshToken = resp.data['refreshToken'] as String;
        await _storage.saveAccessToken(newAccessToken);
        await _storage.saveRefreshToken(newRefreshToken);

        final retryOptions = err.requestOptions;
        retryOptions.headers['Authorization'] = 'Bearer $newAccessToken';
        final retryResp = await _refreshDio.fetch(retryOptions);
        handler.resolve(retryResp);
      } catch (_) {
        await _forceLogout(handler, err);
      }
    } else {
      handler.next(err);
    }
  }

  Future<void> _forceLogout(ErrorInterceptorHandler handler, DioException err) async {
    await _storage.clearAll();
    _authEventBus.forceLogout();
    handler.next(err);
  }
}
