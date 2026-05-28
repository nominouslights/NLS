import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:get_it/get_it.dart';
import '../auth/auth_event_bus.dart';
import '../network/dio_client.dart';
import '../storage/secure_storage_service.dart';
import '../../features/auth/data/datasources/auth_remote_datasource.dart';
import '../../features/auth/data/repositories/auth_repository_impl.dart';
import '../../features/auth/domain/repositories/i_auth_repository.dart';
import '../../features/auth/domain/usecases/login_usecase.dart';
import '../../features/auth/domain/usecases/register_usecase.dart';
import '../../features/users/data/datasources/users_remote_datasource.dart';
import '../../features/users/data/repositories/users_repository_impl.dart';
import '../../features/users/domain/repositories/i_users_repository.dart';
import '../../features/clients/data/datasources/client_remote_datasource.dart';
import '../../features/clients/data/datasources/contract_remote_datasource.dart';
import '../../features/clients/data/repositories/client_repository_impl.dart';
import '../../features/clients/data/repositories/contract_repository_impl.dart';
import '../../features/clients/domain/repositories/i_client_repository.dart';
import '../../features/clients/domain/repositories/i_contract_repository.dart';
import '../../features/clients/domain/usecases/add_rate_line_usecase.dart';
import '../../features/clients/domain/usecases/create_client_usecase.dart';
import '../../features/clients/domain/usecases/create_contract_usecase.dart';
import '../../features/clients/domain/usecases/delete_client_usecase.dart';
import '../../features/clients/domain/usecases/delete_rate_line_usecase.dart';
import '../../features/clients/domain/usecases/get_client_by_id_usecase.dart';
import '../../features/clients/domain/usecases/get_clients_usecase.dart';
import '../../features/clients/domain/usecases/get_contracts_by_client_usecase.dart';
import '../../features/clients/domain/usecases/get_rate_lines_by_client_usecase.dart';
import '../../features/clients/domain/usecases/update_client_usecase.dart';
import '../../features/users/domain/usecases/approve_user_usecase.dart';
import '../../features/users/domain/usecases/get_pending_users_usecase.dart';
import '../../features/users/domain/usecases/reject_user_usecase.dart';
import '../../features/drivers/data/datasources/driver_remote_datasource.dart';
import '../../features/drivers/data/repositories/driver_repository_impl.dart';
import '../../features/drivers/domain/repositories/i_driver_repository.dart';
import '../../features/drivers/domain/usecases/create_driver_usecase.dart';
import '../../features/drivers/domain/usecases/update_driver_usecase.dart';
import '../../features/drivers/domain/usecases/delete_driver_usecase.dart';
import '../../features/drivers/domain/usecases/set_driver_status_usecase.dart';
import '../../features/drivers/domain/usecases/get_drivers_usecase.dart';
import '../../features/drivers/domain/usecases/get_driver_by_id_usecase.dart';
import '../../features/drivers/domain/usecases/get_driver_documents_usecase.dart';
import '../../features/drivers/domain/usecases/upload_driver_document_usecase.dart';
import '../../features/drivers/domain/usecases/download_driver_document_usecase.dart';
import '../../features/drivers/domain/usecases/delete_driver_document_usecase.dart';
import '../../features/drivers/domain/usecases/get_driver_roster_usecase.dart';
import '../../features/drivers/domain/usecases/get_fleet_roster_usecase.dart';
import '../../features/drivers/domain/usecases/upsert_roster_entry_usecase.dart';
import '../../features/drivers/domain/usecases/delete_roster_entry_usecase.dart';
import '../../features/trips/data/datasources/trip_remote_datasource.dart';
import '../../features/trips/data/repositories/trip_repository_impl.dart';
import '../../features/trips/domain/repositories/i_trip_repository.dart';
import '../../features/trips/domain/usecases/get_trips_usecase.dart';
import '../../features/trips/domain/usecases/get_trip_by_id_usecase.dart';
import '../../features/trips/domain/usecases/create_trip_usecase.dart';
import '../../features/trips/domain/usecases/update_trip_usecase.dart';
import '../../features/trips/domain/usecases/delete_trip_usecase.dart';
import '../../features/trips/domain/usecases/assign_driver_usecase.dart';
import '../../features/trips/domain/usecases/dispatch_trip_usecase.dart';
import '../../features/trips/domain/usecases/update_trip_status_usecase.dart';
import '../../features/trips/domain/usecases/submit_pre_inspection_usecase.dart';
import '../../features/trips/domain/usecases/submit_post_report_usecase.dart';
import '../../features/vehicles/data/datasources/vehicle_remote_datasource.dart';
import '../../features/vehicles/data/repositories/vehicle_repository_impl.dart';
import '../../features/vehicles/domain/repositories/i_vehicle_repository.dart';
import '../../features/vehicles/domain/usecases/get_vehicles_usecase.dart';
import '../../features/vehicles/domain/usecases/get_vehicle_by_id_usecase.dart';
import '../../features/vehicles/domain/usecases/create_vehicle_usecase.dart';
import '../../features/vehicles/domain/usecases/update_vehicle_usecase.dart';
import '../../features/vehicles/domain/usecases/delete_vehicle_usecase.dart';
import '../../features/vehicles/domain/usecases/set_vehicle_status_usecase.dart';
import '../../features/vehicles/domain/usecases/set_vehicle_out_of_service_usecase.dart';
import '../../features/vehicles/domain/usecases/update_odometer_usecase.dart';
import '../../features/vehicles/domain/usecases/add_service_record_usecase.dart';
import '../../features/vehicles/domain/usecases/update_service_record_usecase.dart';
import '../../features/vehicles/domain/usecases/complete_service_record_usecase.dart';
import '../../features/vehicles/domain/usecases/delete_service_record_usecase.dart';
import '../../features/vehicles/domain/usecases/add_inspection_record_usecase.dart';
import '../../features/vehicles/domain/usecases/update_inspection_record_usecase.dart';
import '../../features/vehicles/domain/usecases/delete_inspection_record_usecase.dart';
import '../../features/locations/data/datasources/location_remote_datasource.dart';
import '../../features/locations/data/repositories/location_repository_impl.dart';
import '../../features/locations/domain/repositories/i_location_repository.dart';
import '../../features/locations/domain/usecases/get_locations_usecase.dart';
import '../../features/locations/domain/usecases/create_location_usecase.dart';
import '../../features/locations/domain/usecases/update_location_usecase.dart';
import '../../features/locations/domain/usecases/delete_location_usecase.dart';

final sl = GetIt.instance;

Future<void> initDependencies() async {
  sl.registerLazySingleton<FlutterSecureStorage>(
    () => const FlutterSecureStorage(),
  );
  sl.registerLazySingleton<SecureStorageService>(
    () => SecureStorageService(sl()),
  );
  sl.registerLazySingleton<AuthEventBus>(() => AuthEventBus());
  sl.registerLazySingleton<Dio>(() => buildDioClient(sl(), sl()));

  // Auth feature
  sl.registerLazySingleton<IAuthRemoteDataSource>(
    () => AuthRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IAuthRepository>(
    () => AuthRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => LoginUseCase(sl()));
  sl.registerLazySingleton(() => RegisterUseCase(sl()));

  // Users feature
  sl.registerLazySingleton<IUsersRemoteDataSource>(
    () => UsersRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IUsersRepository>(
    () => UsersRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetPendingUsersUseCase(sl()));
  sl.registerLazySingleton(() => ApproveUserUseCase(sl()));
  sl.registerLazySingleton(() => RejectUserUseCase(sl()));

  // Clients feature
  sl.registerLazySingleton<IClientRemoteDataSource>(
    () => ClientRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IContractRemoteDataSource>(
    () => ContractRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IClientRepository>(
    () => ClientRepositoryImpl(sl()),
  );
  sl.registerLazySingleton<IContractRepository>(
    () => ContractRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetClientsUseCase(sl()));
  sl.registerLazySingleton(() => GetClientByIdUseCase(sl()));
  sl.registerLazySingleton(() => CreateClientUseCase(sl()));
  sl.registerLazySingleton(() => UpdateClientUseCase(sl()));
  sl.registerLazySingleton(() => DeleteClientUseCase(sl()));
  sl.registerLazySingleton(() => GetContractsByClientUseCase(sl()));
  sl.registerLazySingleton(() => CreateContractUseCase(sl()));
  sl.registerLazySingleton(() => AddRateLineUseCase(sl()));
  sl.registerLazySingleton(() => DeleteRateLineUseCase(sl()));
  sl.registerLazySingleton(() => GetRateLinesByClientUseCase(sl()));

  // Drivers feature
  sl.registerLazySingleton<IDriverRemoteDataSource>(
    () => DriverRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IDriverRepository>(
    () => DriverRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetDriversUseCase(sl()));
  sl.registerLazySingleton(() => GetDriverByIdUseCase(sl()));
  sl.registerLazySingleton(() => CreateDriverUseCase(sl()));
  sl.registerLazySingleton(() => UpdateDriverUseCase(sl()));
  sl.registerLazySingleton(() => DeleteDriverUseCase(sl()));
  sl.registerLazySingleton(() => SetDriverStatusUseCase(sl()));
  sl.registerLazySingleton(() => GetDriverDocumentsUseCase(sl()));
  sl.registerLazySingleton(() => UploadDriverDocumentUseCase(sl()));
  sl.registerLazySingleton(() => DownloadDriverDocumentUseCase(sl()));
  sl.registerLazySingleton(() => DeleteDriverDocumentUseCase(sl()));
  sl.registerLazySingleton(() => GetDriverRosterUseCase(sl()));
  sl.registerLazySingleton(() => GetFleetRosterUseCase(sl()));
  sl.registerLazySingleton(() => UpsertRosterEntryUseCase(sl()));
  sl.registerLazySingleton(() => DeleteRosterEntryUseCase(sl()));

  // Trips feature
  sl.registerLazySingleton<ITripRemoteDataSource>(
    () => TripRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<ITripRepository>(
    () => TripRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetTripsUseCase(sl()));
  sl.registerLazySingleton(() => GetTripByIdUseCase(sl()));
  sl.registerLazySingleton(() => CreateTripUseCase(sl()));
  sl.registerLazySingleton(() => UpdateTripUseCase(sl()));
  sl.registerLazySingleton(() => DeleteTripUseCase(sl()));
  sl.registerLazySingleton(() => AssignDriverUseCase(sl()));
  sl.registerLazySingleton(() => DispatchTripUseCase(sl()));
  sl.registerLazySingleton(() => UpdateTripStatusUseCase(sl()));
  sl.registerLazySingleton(() => SubmitPreInspectionUseCase(sl()));
  sl.registerLazySingleton(() => SubmitPostReportUseCase(sl()));

  // Vehicles feature
  sl.registerLazySingleton<IVehicleRemoteDataSource>(
    () => VehicleRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<IVehicleRepository>(
    () => VehicleRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetVehiclesUseCase(sl()));
  sl.registerLazySingleton(() => GetVehicleByIdUseCase(sl()));
  sl.registerLazySingleton(() => CreateVehicleUseCase(sl()));
  sl.registerLazySingleton(() => UpdateVehicleUseCase(sl()));
  sl.registerLazySingleton(() => DeleteVehicleUseCase(sl()));
  sl.registerLazySingleton(() => SetVehicleStatusUseCase(sl()));
  sl.registerLazySingleton(() => SetVehicleOutOfServiceUseCase(sl()));
  sl.registerLazySingleton(() => UpdateOdometerUseCase(sl()));
  sl.registerLazySingleton(() => AddServiceRecordUseCase(sl()));
  sl.registerLazySingleton(() => UpdateServiceRecordUseCase(sl()));
  sl.registerLazySingleton(() => CompleteServiceRecordUseCase(sl()));
  sl.registerLazySingleton(() => DeleteServiceRecordUseCase(sl()));
  sl.registerLazySingleton(() => AddInspectionRecordUseCase(sl()));
  sl.registerLazySingleton(() => UpdateInspectionRecordUseCase(sl()));
  sl.registerLazySingleton(() => DeleteInspectionRecordUseCase(sl()));

  // Locations feature
  sl.registerLazySingleton<ILocationRemoteDataSource>(
    () => LocationRemoteDataSource(sl()),
  );
  sl.registerLazySingleton<ILocationRepository>(
    () => LocationRepositoryImpl(sl()),
  );
  sl.registerLazySingleton(() => GetLocationsUseCase(sl()));
  sl.registerLazySingleton(() => CreateLocationUseCase(sl()));
  sl.registerLazySingleton(() => UpdateLocationUseCase(sl()));
  sl.registerLazySingleton(() => DeleteLocationUseCase(sl()));
}
