import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/driver_roster_entry.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../../domain/usecases/delete_roster_entry_usecase.dart';
import '../../domain/usecases/get_driver_roster_usecase.dart';
import '../../domain/usecases/upsert_roster_entry_usecase.dart';

class DriverRosterKey extends Equatable {
  final String driverId;
  final DateTime rangeStart;
  final DateTime rangeEnd;

  const DriverRosterKey(this.driverId, this.rangeStart, this.rangeEnd);

  @override
  List<Object?> get props => [driverId, rangeStart, rangeEnd];
}

final driverRosterProvider = AsyncNotifierProvider.family<
    DriverRosterNotifier, List<DriverRosterEntry>, DriverRosterKey>(
  DriverRosterNotifier.new,
);

class DriverRosterNotifier
    extends FamilyAsyncNotifier<List<DriverRosterEntry>, DriverRosterKey> {
  @override
  Future<List<DriverRosterEntry>> build(DriverRosterKey key) => _load(key);

  Future<List<DriverRosterEntry>> _load(DriverRosterKey key) async {
    final result = await sl<GetDriverRosterUseCase>()(
      GetRosterParams(key.driverId, key.rangeStart, key.rangeEnd),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (entries) => entries,
    );
  }

  Future<String> upsertEntry(UpsertRosterEntryParams params) async {
    final result = await sl<UpsertRosterEntryUseCase>()(
      UpsertRosterUseCaseParams(arg.driverId, params),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (id) {
        ref.invalidateSelf();
        return id;
      },
    );
  }

  Future<void> deleteEntry(String entryId) async {
    final result = await sl<DeleteRosterEntryUseCase>()(
      DeleteRosterEntryParams(arg.driverId, entryId),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}
