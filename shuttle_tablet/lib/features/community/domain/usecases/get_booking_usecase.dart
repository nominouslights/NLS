import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/community_booking.dart';
import '../repositories/i_community_repository.dart';

class GetBookingUseCase implements UseCase<CommunityBooking, String> {
  final ICommunityRepository _repository;
  const GetBookingUseCase(this._repository);

  @override
  Future<Either<Failure, CommunityBooking>> call(String reference) =>
      _repository.getBookingByReference(reference);
}
