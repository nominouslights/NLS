import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/passenger_profile.dart';
import '../repositories/i_passenger_profile_repository.dart';

class SearchPassengerProfilesUseCase {
  final IPassengerProfileRepository _repository;
  const SearchPassengerProfilesUseCase(this._repository);

  Future<Either<Failure, List<PassengerProfile>>> call(
          String clientId, String query) =>
      _repository.search(clientId, query);
}
