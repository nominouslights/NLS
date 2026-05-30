import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/calendar_day.dart';
import '../entities/community_booking.dart';

abstract interface class ICommunityRepository {
  Future<Either<Failure, List<CalendarDay>>> getCalendar({bool isAdmin = false});

  Future<Either<Failure, CommunityBooking>> bookSeat(BookSeatParams params);

  Future<Either<Failure, CommunityBooking>> getBookingByReference(
      String reference);

  Future<Either<Failure, int>> blockDay(BlockDayParams params);

  Future<Either<Failure, void>> unblockDay(String date);
}

class BookSeatParams {
  final DateTime date;
  final String direction;
  final String tripType;
  final String fullName;
  final String phone;
  final String email;

  const BookSeatParams({
    required this.date,
    required this.direction,
    required this.tripType,
    required this.fullName,
    required this.phone,
    required this.email,
  });
}

class BlockDayParams {
  final String date;
  final String reason;

  const BlockDayParams({required this.date, required this.reason});
}
