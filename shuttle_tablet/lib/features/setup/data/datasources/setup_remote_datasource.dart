import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';

abstract interface class ISetupRemoteDataSource {
  Future<bool> getSetupStatus();
  Future<void> initializeSystem(String email, String password);
}

class SetupRemoteDataSource implements ISetupRemoteDataSource {
  final Dio _dio;
  const SetupRemoteDataSource(this._dio);

  @override
  Future<bool> getSetupStatus() async {
    try {
      final response = await _dio.get(ApiEndpoints.setupStatus);
      final data = response.data as Map<String, dynamic>;
      return data['isSetupComplete'] as bool;
    } on DioException catch (e) {
      throw ServerException(
        message: e.message ?? 'Failed to get setup status',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> initializeSystem(String email, String password) async {
    try {
      await _dio.post(
        ApiEndpoints.setupInitialize,
        data: {'email': email, 'password': password},
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 409) {
        throw const ConflictException('System has already been initialized.');
      }
      throw ServerException(
        message: e.message ?? 'Initialization failed',
        statusCode: e.response?.statusCode,
      );
    }
  }
}
