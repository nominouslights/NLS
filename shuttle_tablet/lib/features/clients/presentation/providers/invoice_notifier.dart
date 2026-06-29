import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/invoice.dart';
import '../../domain/usecases/create_invoice_usecase.dart';
import '../../domain/usecases/get_invoices_by_client_usecase.dart';
import '../../domain/usecases/mark_invoice_paid_usecase.dart';
import '../../domain/usecases/mark_invoice_sent_usecase.dart';
import '../../domain/usecases/void_invoice_usecase.dart';

final invoiceNotifierProvider =
    AsyncNotifierProviderFamily<InvoiceNotifier, List<Invoice>, String>(
  InvoiceNotifier.new,
);

class InvoiceNotifier extends FamilyAsyncNotifier<List<Invoice>, String> {
  @override
  Future<List<Invoice>> build(String clientId) => _load(clientId);

  Future<List<Invoice>> _load(String clientId) async {
    final result = await sl<GetInvoicesByClientUseCase>()(
      GetInvoicesByClientParams(clientId: clientId),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (invoices) => invoices,
    );
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => _load(arg));
  }

  Future<Invoice> createInvoice(
    String clientId,
    List<String> tripIds,
    String? notes,
  ) async {
    final result = await sl<CreateInvoiceUseCase>()(
      CreateInvoiceParams(clientId: clientId, tripIds: tripIds, notes: notes),
    );
    return result.fold(
      (failure) => throw Exception(failure.message),
      (invoice) {
        ref.invalidateSelf();
        return invoice;
      },
    );
  }

  Future<void> markSent(String invoiceId) async {
    final result = await sl<MarkInvoiceSentUseCase>()(invoiceId);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> markPaid(String invoiceId) async {
    final result = await sl<MarkInvoicePaidUseCase>()(invoiceId);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }

  Future<void> voidInvoice(String invoiceId) async {
    final result = await sl<VoidInvoiceUseCase>()(invoiceId);
    result.fold(
      (failure) => throw Exception(failure.message),
      (_) => ref.invalidateSelf(),
    );
  }
}
