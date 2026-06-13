import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../models/passenger_profile_model.dart';

abstract interface class IPassengerProfileRemoteDataSource {
  Future<List<PassengerProfileModel>> search(String clientId, String query);
}

class PassengerProfileRemoteDataSource
    implements IPassengerProfileRemoteDataSource {
  final Dio _dio;
  const PassengerProfileRemoteDataSource(this._dio);

  @override
  Future<List<PassengerProfileModel>> search(
      String clientId, String query) async {
    try {
      final response = await _dio
          .get(ApiEndpoints.passengerSearch(clientId, query));
      final list = response.data as List<dynamic>;
      return list
          .map((e) => PassengerProfileModel.fromJson(
              e as Map<String, dynamic>, clientId))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to search passenger profiles',
        statusCode: e.response?.statusCode,
      );
    }
  }
}
