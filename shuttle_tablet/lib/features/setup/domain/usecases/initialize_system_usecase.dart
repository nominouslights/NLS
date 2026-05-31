import 'package:equatable/equatable.dart';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../repositories/i_setup_repository.dart';

class InitializeSystemUseCase implements UseCase<void, InitializeParams> {
  final ISetupRepository _repository;
  const InitializeSystemUseCase(this._repository);

  @override
  Future<Either<Failure, void>> call(InitializeParams params) =>
      _repository.initializeSystem(params.email, params.password);
}

class InitializeParams extends Equatable {
  final String email;
  final String password;
  const InitializeParams({required this.email, required this.password});

  @override
  List<Object?> get props => [email, password];
}
