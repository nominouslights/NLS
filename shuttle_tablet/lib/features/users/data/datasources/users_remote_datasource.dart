import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../models/pending_user_model.dart';

abstract interface class IUsersRemoteDataSource {
  Future<List<PendingUserModel>> getPendingUsers();
  Future<void> approveUser(String id);
  Future<void> rejectUser(String id);
}

class UsersRemoteDataSource implements IUsersRemoteDataSource {
  final Dio _dio;
  const UsersRemoteDataSource(this._dio);

  @override
  Future<List<PendingUserModel>> getPendingUsers() async {
    try {
      final response = await _dio.get(ApiEndpoints.pendingUsers);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => PendingUserModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load pending users',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> approveUser(String id) async {
    try {
      await _dio.post(ApiEndpoints.approveUser(id));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to approve user',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> rejectUser(String id) async {
    try {
      await _dio.post(ApiEndpoints.rejectUser(id));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to reject user',
        statusCode: e.response?.statusCode,
      );
    }
  }
}
