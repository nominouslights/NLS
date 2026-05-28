import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/usecases/get_trips_usecase.dart';

final vehicleTripsProvider =
    FutureProvider.family<List<Trip>, String>((ref, vehicleId) async {
  final result = await sl<GetTripsUseCase>()(GetTripsParams(vehicleId: vehicleId));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (trips) => trips,
  );
});
