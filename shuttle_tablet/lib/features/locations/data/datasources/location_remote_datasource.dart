import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/repositories/i_location_repository.dart';
import '../models/saved_location_model.dart';

abstract interface class ILocationRemoteDataSource {
  Future<List<SavedLocationModel>> getLocations();
  Future<String> createLocation(CreateLocationParams params);
  Future<void> updateLocation(String id, UpdateLocationParams params);
  Future<void> deleteLocation(String id);
}

class LocationRemoteDataSource implements ILocationRemoteDataSource {
  final Dio _dio;
  const LocationRemoteDataSource(this._dio);

  @override
  Future<List<SavedLocationModel>> getLocations() async {
    try {
      final response = await _dio.get(ApiEndpoints.locations);
      final list = response.data as List<dynamic>;
      return list
          .map((e) => SavedLocationModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to load locations',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<String> createLocation(CreateLocationParams params) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.locations,
        data: {
          'name': params.name,
          'address': params.address,
          'latitude': params.latitude,
          'longitude': params.longitude,
        },
      );
      final data = response.data as Map<String, dynamic>;
      return data['id'] as String;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      throw ServerException(
        message: e.message ?? 'Failed to create location',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> updateLocation(String id, UpdateLocationParams params) async {
    try {
      await _dio.put(
        ApiEndpoints.locationById(id),
        data: {
          'name': params.name,
          'address': params.address,
          'latitude': params.latitude,
          'longitude': params.longitude,
        },
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to update location',
        statusCode: e.response?.statusCode,
      );
    }
  }

  @override
  Future<void> deleteLocation(String id) async {
    try {
      await _dio.delete(ApiEndpoints.locationById(id));
    } on DioException catch (e) {
      if (e.response?.statusCode == 401) throw const UnauthorizedException();
      if (e.response?.statusCode == 404) throw const NotFoundException();
      throw ServerException(
        message: e.message ?? 'Failed to delete location',
        statusCode: e.response?.statusCode,
      );
    }
  }
}
