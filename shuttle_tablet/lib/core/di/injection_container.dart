import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:get_it/get_it.dart';
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

final sl = GetIt.instance;

Future<void> initDependencies() async {
  sl.registerLazySingleton<FlutterSecureStorage>(
    () => const FlutterSecureStorage(),
  );
  sl.registerLazySingleton<SecureStorageService>(
    () => SecureStorageService(sl()),
  );
  sl.registerLazySingleton<Dio>(() => buildDioClient(sl()));

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
}
