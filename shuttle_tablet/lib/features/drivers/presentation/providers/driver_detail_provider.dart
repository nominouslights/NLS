import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/driver.dart';
import '../../domain/usecases/get_driver_by_id_usecase.dart';

final driverDetailProvider =
    FutureProvider.family<Driver, String>((ref, driverId) async {
  final result = await sl<GetDriverByIdUseCase>()(DriverIdParams(driverId));
  return result.fold(
    (failure) => throw Exception(failure.message),
    (driver) => driver,
  );
});
