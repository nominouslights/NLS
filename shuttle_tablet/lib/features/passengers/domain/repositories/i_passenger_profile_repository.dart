import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/passenger_profile.dart';

abstract interface class IPassengerProfileRepository {
  Future<Either<Failure, List<PassengerProfile>>> search(
      String clientId, String query);
}
