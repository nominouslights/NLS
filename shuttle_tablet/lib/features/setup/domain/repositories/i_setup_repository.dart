import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';

abstract interface class ISetupRepository {
  Future<Either<Failure, bool>> getSetupStatus();
  Future<Either<Failure, void>> initializeSystem(String email, String password);
}
