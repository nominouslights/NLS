import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/client_email_template.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../../domain/usecases/get_client_email_templates_usecase.dart';
import '../../domain/usecases/upsert_client_email_template_usecase.dart';

final clientEmailTemplatesProvider = AsyncNotifierProviderFamily<
    ClientEmailTemplatesNotifier, List<ClientEmailTemplate>, String>(
  ClientEmailTemplatesNotifier.new,
);

class ClientEmailTemplatesNotifier
    extends FamilyAsyncNotifier<List<ClientEmailTemplate>, String> {
  @override
  Future<List<ClientEmailTemplate>> build(String clientId) => _load(clientId);

  Future<List<ClientEmailTemplate>> _load(String clientId) async {
    final result = await sl<GetClientEmailTemplatesUseCase>()(clientId);
    return result.fold(
      (failure) => throw Exception(failure.message),
      (templates) => templates,
    );
  }

  Future<void> upsert(UpsertEmailTemplateParams params) async {
    final result = await sl<UpsertClientEmailTemplateUseCase>()(params);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}
