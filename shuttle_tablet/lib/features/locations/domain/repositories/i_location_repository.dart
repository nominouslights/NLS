import 'package:fpdart/fpdart.dart';
import '../../../../core/error/failures.dart';
import '../entities/saved_location.dart';

abstract interface class ILocationRepository {
  Future<Either<Failure, List<SavedLocation>>> getLocations();
  Future<Either<Failure, String>> createLocation(CreateLocationParams params);
  Future<Either<Failure, void>> updateLocation(String id, UpdateLocationParams params);
  Future<Either<Failure, void>> deleteLocation(String id);
}

class CreateLocationParams {
  final String name;
  final String? address;
  final double? latitude;
  final double? longitude;

  const CreateLocationParams({
    required this.name,
    this.address,
    this.latitude,
    this.longitude,
  });
}

class UpdateLocationParams {
  final String name;
  final String? address;
  final double? latitude;
  final double? longitude;

  const UpdateLocationParams({
    required this.name,
    this.address,
    this.latitude,
    this.longitude,
  });
}
