import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../models/auth_token_model.dart';

abstract interface class IAuthRemoteDataSource {
  Future<AuthTokenModel> login(String email, String password);
  Future<AuthTokenModel> refreshToken(String refreshToken);
  Future<void> register(String email, String password, String role);
  Future<void> changePassword(String currentPassword, String newPassword);
}

class AuthRemoteDataSource implements IAuthRemoteDataSource {
  final Dio _dio;
  const AuthRemoteDataSource(this._dio);

  @override
  Future<AuthTokenModel> login(String email, String password) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.login,
        data: {'email': email, 'password': password},
      );
      return AuthTokenModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Login failed',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<AuthTokenModel> refreshToken(String refreshToken) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.refresh,
        data: {'refreshToken': refreshToken},
      );
      return AuthTokenModel.fromJson(response.data as Map<String, dynamic>);
    } on DioException catch (e) {
      throw ServerException(
        message: e.message ?? 'Token refresh failed',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> register(String email, String password, String role) async {
    try {
      await _dio.post(
        ApiEndpoints.register,
        data: {'email': email, 'password': password, 'role': role},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 409) {
        throw ConflictException(
          (e.response?.data as Map<String, dynamic>?)?['error'] as String? ??
              'Email already in use.',
        );
      }
      throw ServerException(
        message: e.message ?? 'Registration failed',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> changePassword(String currentPassword, String newPassword) async {
    try {
      await _dio.post(
        ApiEndpoints.changePassword,
        data: {'currentPassword': currentPassword, 'newPassword': newPassword},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Password change failed',
        statusCode: e.response?.statusCode,
      );
    }
  }
}
