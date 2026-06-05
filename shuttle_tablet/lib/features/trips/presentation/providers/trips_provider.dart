import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/delay_entry.dart';
import '../../domain/entities/trip.dart';
import '../../domain/usecases/delete_trip_usecase.dart';
import '../../domain/usecases/dispatch_trip_usecase.dart';
import '../../domain/usecases/get_trip_by_id_usecase.dart';
import '../../domain/usecases/get_trips_usecase.dart';
import '../../domain/usecases/update_trip_status_usecase.dart';

final tripsProvider =
    AsyncNotifierProvider<TripsNotifier, List<Trip>>(TripsNotifier.new);

class TripsNotifier extends AsyncNotifier<List<Trip>> {
  TripStatus? _statusFilter;
  String? _clientIdFilter;
  String? _driverIdFilter;
  TripServiceType? _serviceTypeFilter;

  @override
  Future<List<Trip>> build() => _load();

  Future<List<Trip>> _load() async {
    final result = await sl<GetTripsUseCase>()(GetTripsParams(
      status: _statusFilter,
      clientId: _clientIdFilter,
      driverId: _driverIdFilter,
      serviceType: _serviceTypeFilter,
    ));
    return result.fold(
      (failure) => throw Exception(failure.message),
      (trips) => trips,
    );
  }

  void setFilter({
    TripStatus? status,
    String? clientId,
    String? driverId,
    TripServiceType? serviceType,
  }) {
    _statusFilter = status;
    _clientIdFilter = clientId;
    _driverIdFilter = driverId;
    _serviceTypeFilter = serviceType;
    ref.invalidateSelf();
  }

  Future<void> deleteTrip(String id) async {
    final result = await sl<DeleteTripUseCase>()(TripIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> dispatchTrip(String id) async {
    final result = await sl<DispatchTripUseCase>()(TripIdParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> updateStatus(String id, TripStatus status) async {
    final result = await sl<UpdateTripStatusUseCase>()(
      UpdateTripStatusParams(tripId: id, status: status),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

final tripDetailProvider =
    FutureProvider.family<Trip, String>((ref, id) async {
  final result = await sl<GetTripByIdUseCase>()(TripIdParams(id));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (trip) => trip,
  );
});

// Tracks which stop index the driver is currently at during execution (client-side only).
final currentStopIndexProvider =
    StateProvider.family<int, String>((ref, tripId) => 0);

// Accumulates delay entries logged mid-trip (client-side only, passed to post-trip report).
final tripDelayLogsProvider =
    StateProvider.family<List<DelayEntry>, String>((ref, tripId) => []);
