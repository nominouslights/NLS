import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/passenger_profile.dart';
import '../../domain/repositories/i_passenger_profile_repository.dart';
import '../datasources/passenger_profile_remote_datasource.dart';

class PassengerProfileRepositoryImpl implements IPassengerProfileRepository {
  final IPassengerProfileRemoteDataSource _remote;
  const PassengerProfileRepositoryImpl(this._remote);

  @override
  Future<Either<Failure, List<PassengerProfile>>> search(
      String clientId, String query) async {
    try {
      final models = await _remote.search(clientId, query);
      return Right(models);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}
