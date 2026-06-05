import 'package:fpdart/fpdart.dart';
import '../../../../core/error/exceptions.dart';
import '../../../../core/error/failures.dart';
import '../../domain/entities/client.dart';
import '../../domain/entities/client_email_template.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../datasources/client_remote_datasource.dart';

class ClientRepositoryImpl implements IClientRepository {
  final IClientRemoteDataSource _remoteDataSource;
  const ClientRepositoryImpl(this._remoteDataSource);

  @override
  Future<Either<Failure, List<Client>>> getClients() async {
    try {
      final result = await _remoteDataSource.getClients();
      return Right(result);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, Client>> getClientById(String id) async {
    try {
      final result = await _remoteDataSource.getClientById(id);
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
  Future<Either<Failure, String>> createClient(CreateClientParams params) async {
    try {
      final id = await _remoteDataSource.createClient(params);
      return Right(id);
    } on UnauthorizedException {
      return const Left(UnauthorizedFailure());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    }
  }

  @override
  Future<Either<Failure, void>> updateClient(String id, UpdateClientParams params) async {
    try {
      await _remoteDataSource.updateClient(id, params);
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
  Future<Either<Failure, void>> deleteClient(String id) async {
    try {
      await _remoteDataSource.deleteClient(id);
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
  Future<Either<Failure, List<ClientEmailTemplate>>> getEmailTemplates(
      String clientId) async {
    try {
      final result = await _remoteDataSource.getEmailTemplates(clientId);
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
  Future<Either<Failure, void>> upsertEmailTemplate(
      UpsertEmailTemplateParams params) async {
    try {
      await _remoteDataSource.upsertEmailTemplate(
        params.clientId,
        params.type,
        params.subject,
        params.body,
      );
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
