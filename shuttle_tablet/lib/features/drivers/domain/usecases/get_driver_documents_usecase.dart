import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/driver_document.dart';
import '../repositories/i_driver_repository.dart';
import 'get_driver_by_id_usecase.dart';

class GetDriverDocumentsUseCase implements UseCase<List<DriverDocument>, DriverIdParams> {
  final IDriverRepository _repository;
  const GetDriverDocumentsUseCase(this._repository);

  @override
  Future<Either<Failure, List<DriverDocument>>> call(DriverIdParams params) =>
      _repository.getDriverDocuments(params.id);
}
