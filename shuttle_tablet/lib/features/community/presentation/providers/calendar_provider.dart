import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/calendar_day.dart';
import '../../domain/usecases/get_calendar_usecase.dart';

final calendarProvider =
    AsyncNotifierProvider<CalendarNotifier, List<CalendarDay>>(
        CalendarNotifier.new);

class CalendarNotifier extends AsyncNotifier<List<CalendarDay>> {
  @override
  Future<List<CalendarDay>> build() => _load(isAdmin: false);

  Future<List<CalendarDay>> _load({required bool isAdmin}) async {
    final result = await sl<GetCalendarUseCase>()(
        GetCalendarParams(isAdmin: isAdmin));
    return result.fold(
      (failure) => throw Exception(failure.message),
      (days) => days,
    );
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

final adminCalendarProvider =
    AsyncNotifierProvider<AdminCalendarNotifier, List<CalendarDay>>(
        AdminCalendarNotifier.new);

class AdminCalendarNotifier extends AsyncNotifier<List<CalendarDay>> {
  @override
  Future<List<CalendarDay>> build() => _load();

  Future<List<CalendarDay>> _load() async {
    final result =
        await sl<GetCalendarUseCase>()(const GetCalendarParams(isAdmin: true));
    return result.fold(
      (failure) => throw Exception(failure.message),
      (days) => days,
    );
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}
