import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/contract.dart';
import '../../domain/entities/contract_rate_line.dart';
import '../../domain/repositories/i_contract_repository.dart';
import '../datasources/contract_remote_datasource.dart';

class ContractRepositoryImpl implements IContractRepository {
  final IContractRemoteDataSource _remoteDataSource;
  const ContractRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<Contract>>> getContractsByClientId(String clientId) async {
    try {
      final result = await _remoteDataSource.getContractsByClientId(clientId);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> createContract(CreateContractParams params) async {
    try {
      final id = await _remoteDataSource.createContract(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateContract(String contractId, UpdateContractParams params) async {
    try {
      // clientId is not tracked at the domain level; we pass empty string and resolve via the datasource
      await _remoteDataSource.updateContract(contractId, '', params);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, String>> addRateLine(AddRateLineParams params) async {
    try {
      final id = await _remoteDataSource.addRateLine(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> deleteRateLine(String rateLineId, String clientId) async {
    try {
      await _remoteDataSource.deleteRateLine(rateLineId, clientId);
      return const Right(null);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on NotFoundException {
      return const Left(NotFoundFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, List<ContractRateLine>>> getRateLinesByClientId(String clientId) async {
    try {
      final result = await _remoteDataSource.getRateLinesByClientId(clientId);
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }
}
