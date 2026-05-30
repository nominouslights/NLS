import 'package:dio/dio.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/network/api_endpoints.dart';
import '../../domain/repositories/i_community_repository.dart';
import '../models/calendar_day_model.dart';
import '../models/community_booking_model.dart';

abstract interface class ICommunityRemoteDataSource {
  Future<List<CalendarDayModel>> getCalendar({bool isAdmin = false});
  Future<CommunityBookingModel> bookSeat(BookSeatParams params);
  Future<CommunityBookingModel> getBookingByReference(String reference);
  Future<int> blockDay(BlockDayParams params);
  Future<void> unblockDay(String date);
}

class CommunityRemoteDataSource implements ICommunityRemoteDataSource {
  final Dio _dio;
  const CommunityRemoteDataSource(this._dio);

  @override
  Future<List<CalendarDayModel>> getCalendar({bool isAdmin = false}) async {
    try {
      final endpoint = isAdmin
          ? ApiEndpoints.communityAdminCalendar
          : ApiEndpoints.communityCalendar;
      final response = await _dio.get<List<dynamic>>(endpoint);
      return (response.data ?? [])
          .map((e) => CalendarDayModel.fromJson(e as Map<String, dynamic>))
          .toList();
    } on DioException catch (e) {
      _handleDioError(e);
    }
  }

  @override
  Future<CommunityBookingModel> bookSeat(BookSeatParams params) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.communityBookings,
        data: {
          'date': _formatDate(params.date),
          'direction': params.direction,
          'tripType': params.tripType,
          'fullName': params.fullName,
          'phone': params.phone,
          'email': params.email,
        },
      );
      return CommunityBookingModel.fromJson(response.data!);
    } on DioException catch (e) {
      _handleDioError(e);
    }
  }

  @override
  Future<CommunityBookingModel> getBookingByReference(String reference) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        ApiEndpoints.bookingByRef(reference),
      );
      return CommunityBookingModel.fromJson(response.data!);
    } on DioException catch (e) {
      _handleDioError(e);
    }
  }

  @override
  Future<int> blockDay(BlockDayParams params) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        ApiEndpoints.communityBlocks,
        data: {'date': params.date, 'reason': params.reason},
      );
      return (response.data?['passengersCancelled'] as int?) ?? 0;
    } on DioException catch (e) {
      _handleDioError(e);
    }
  }

  @override
  Future<void> unblockDay(String date) async {
    try {
      await _dio.delete(ApiEndpoints.blockByDate(date));
    } on DioException catch (e) {
      _handleDioError(e);
    }
  }

  static String _formatDate(DateTime date) =>
      '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';

  Never _handleDioError(DioException e) {
    if (e.response?.statusCode == 401) throw const UnauthorizedException();
    if (e.response?.statusCode == 404) throw const NotFoundException();
    throw ServerException(
        message: e.response?.data?['message'] as String? ?? e.message ?? 'Server error');
  }
}
