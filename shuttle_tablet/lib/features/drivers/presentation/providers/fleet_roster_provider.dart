import 'package:equatable/equatable.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../../domain/usecases/get_fleet_roster_usecase.dart';

class FleetRosterKey extends Equatable {
  final DateTime rangeStart;
  final DateTime rangeEnd;

  const FleetRosterKey(this.rangeStart, this.rangeEnd);

  @override
  List<Object?> get props => [rangeStart, rangeEnd];
}

final fleetRosterProvider = AsyncNotifierProvider.family<
    FleetRosterNotifier, List<DriverRosterSummary>, FleetRosterKey>(
  FleetRosterNotifier.new,
);

class FleetRosterNotifier
    extends FamilyAsyncNotifier<List<DriverRosterSummary>, FleetRosterKey> {
  @override
  Future<List<DriverRosterSummary>> build(FleetRosterKey key) => _load(key);

  Future<List<DriverRosterSummary>> _load(FleetRosterKey key) async {
    final result = await sl<GetFleetRosterUseCase>()(
      FleetRosterParams(key.rangeStart, key.rangeEnd),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (summaries) => summaries,
    );
  }

  Future<void> refresh() => ref.refresh(
        fleetRosterProvider(arg).future,
      );
}
