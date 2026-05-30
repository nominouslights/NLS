import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../../../../core/usecases/usecase.dart';
import '../entities/calendar_day.dart';
import '../repositories/i_community_repository.dart';

class GetCalendarParams {
  final bool isAdmin;
  const GetCalendarParams({this.isAdmin = false});
}

class GetCalendarUseCase implements UseCase<List<CalendarDay>, GetCalendarParams> {
  final ICommunityRepository _repository;
  const GetCalendarUseCase(this._repository);

  @override
  Future<Either<Failure, List<CalendarDay>>> call(GetCalendarParams params) =>
      _repository.getCalendar(isAdmin: params.isAdmin);
}
