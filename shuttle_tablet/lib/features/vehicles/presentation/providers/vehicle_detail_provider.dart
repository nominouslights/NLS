import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/vehicle.dart';
import '../../domain/usecases/get_vehicle_by_id_usecase.dart';

final vehicleDetailProvider =
    FutureProvider.family<Vehicle, String>((ref, vehicleId) async {
  final result = await sl<GetVehicleByIdUseCase>()(VehicleIdParams(vehicleId));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (vehicle) => vehicle,
  );
});
