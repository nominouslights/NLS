import 'dart:typed_data';
import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/vehicle_fuel_entry.dart';
import '../entities/vehicle_odometer_entry.dart';
import '../repositories/i_vehicle_repository.dart';

class GetFuelEntriesUseCase {
  final IVehicleRepository _repo;
  const GetFuelEntriesUseCase(this._repo);
  Future<Either<Failure, List<VehicleFuelEntry>>> call(String vehicleId) =>
      _repo.getFuelEntries(vehicleId);
}

class AddFuelEntryUseCase {
  final IVehicleRepository _repo;
  const AddFuelEntryUseCase(this._repo);
  Future<Either<Failure, String>> call(
          String vehicleId, AddFuelEntryParams params) =>
      _repo.addFuelEntry(vehicleId, params);
}

class DeleteFuelEntryUseCase {
  final IVehicleRepository _repo;
  const DeleteFuelEntryUseCase(this._repo);
  Future<Either<Failure, void>> call(String vehicleId, String entryId) =>
      _repo.deleteFuelEntry(vehicleId, entryId);
}

class GetFuelReceiptUseCase {
  final IVehicleRepository _repo;
  const GetFuelReceiptUseCase(this._repo);
  Future<Either<Failure, Uint8List>> call(String vehicleId, String entryId) =>
      _repo.getFuelReceipt(vehicleId, entryId);
}

class GetOdometerHistoryUseCase {
  final IVehicleRepository _repo;
  const GetOdometerHistoryUseCase(this._repo);
  Future<Either<Failure, List<VehicleOdometerEntry>>> call(String vehicleId) =>
      _repo.getOdometerHistory(vehicleId);
}
