import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/pending_user.dart';

abstract interface class IUsersRepository {
  Future<Either<Failure, List<PendingUser>>> getPendingUsers();
  Future<Either<Failure, void>> approveUser(String id);
  Future<Either<Failure, void>> rejectUser(String id);
}
