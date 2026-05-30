import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/community_booking.dart';
import '../repositories/i_community_repository.dart';

class BookSeatUseCase implements UseCase<CommunityBooking, BookSeatParams> {
  final ICommunityRepository _repository;
  const BookSeatUseCase(this._repository);

  @override
  Future<Either<Failure, CommunityBooking>> call(BookSeatParams params) =>
      _repository.bookSeat(params);
}
