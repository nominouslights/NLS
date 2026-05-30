import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/calendar_day.dart';
import '../../domain/entities/community_booking.dart';
import '../../domain/repositories/i_community_repository.dart';
import '../datasources/community_remote_datasource.dart';

class CommunityRepositoryImpl implements ICommunityRepository {
  final ICommunityRemoteDataSource _remoteDataSource;
  const CommunityRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<CalendarDay>>> getCalendar(
      {bool isAdmin = false}) async {
    try {
      final result = await _remoteDataSource.getCalendar(isAdmin: isAdmin);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, CommunityBooking>> bookSeat(
      BookSeatParams params) async {
    try {
      final result = await _remoteDataSource.bookSeat(params);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, CommunityBooking>> getBookingByReference(
      String reference) async {
    try {
      final result = await _remoteDataSource.getBookingByReference(reference);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, int>> blockDay(BlockDayParams params) async {
    try {
      final result = await _remoteDataSource.blockDay(params);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> unblockDay(String date) async {
    try {
      await _remoteDataSource.unblockDay(date);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}
