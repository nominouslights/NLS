import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/usecases/usecase.dart';
import '../../domain/entities/saved_location.dart';
import '../../domain/repositories/i_location_repository.dart';
import '../../domain/usecases/get_locations_usecase.dart';
import '../../domain/usecases/create_location_usecase.dart';
import '../../domain/usecases/update_location_usecase.dart';
import '../../domain/usecases/delete_location_usecase.dart';

final locationsProvider =
    AsyncNotifierProvider<LocationsNotifier, List<SavedLocation>>(LocationsNotifier.new);

class LocationsNotifier extends AsyncNotifier<List<SavedLocation>> {
  @override
  Future<List<SavedLocation>> build() => _load();

  Future<List<SavedLocation>> _load() async {
    final result = await sl<GetLocationsUseCase>()(const NoParams());
    return result.fold(
      (failure) => throw Exception(failure.message),
      (locations) => locations,
    );
  }

  Future<void> createLocation(CreateLocationParams params) async {
    final result = await sl<CreateLocationUseCase>()(params);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> updateLocation(String id, UpdateLocationParams params) async {
    final result = await sl<UpdateLocationUseCase>()(
      UpdateLocationUseCaseParams(id: id, data: params),
    );
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> deleteLocation(String id) async {
    final result = await sl<DeleteLocationUseCase>()(DeleteLocationParams(id));
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> refresh() => ref.refresh(locationsProvider.future);
}
