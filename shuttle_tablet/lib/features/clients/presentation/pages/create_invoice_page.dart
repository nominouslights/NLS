import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/billing_ready_trip.dart';
import '../../domain/usecases/get_billing_ready_trips_usecase.dart';
import '../providers/invoice_notifier.dart';
import '../widgets/billing_ready_trip_card.dart';

class CreateInvoicePage extends ConsumerStatefulWidget {
  final String clientId;
  const CreateInvoicePage({super.key, required this.clientId});

  @override
  ConsumerState<CreateInvoicePage> createState() => _CreateInvoicePageState();
}

class _CreateInvoicePageState extends ConsumerState<CreateInvoicePage> {
  int _step = 0; // 0 = trip selection, 1 = review & notes

  List<BillingReadyTrip> _availableTrips = [];
  final Set<String> _selectedTripIds = {};
  bool _loadingTrips = true;
  String? _loadError;

  final _notesCtrl = TextEditingController();
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _fetchTrips();
  }

  @override
  void dispose() {
    _notesCtrl.dispose();
    super.dispose();
  }

  Future<void> _fetchTrips() async {
    setState(() {
      _loadingTrips = true;
      _loadError = null;
    });
    final result =
        await sl<GetBillingReadyTripsUseCase>()(widget.clientId);
    if (!mounted) return;
    result.fold(
      (failure) => setState(() {
        _loadingTrips = false;
        _loadError = failure.message;
      }),
      (trips) => setState(() {
        _loadingTrips = false;
        _availableTrips = trips;
      }),
    );
  }

  Future<void> _submit({required bool sendImmediately}) async {
    if (_selectedTripIds.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Select at least one trip to invoice.'),
          backgroundColor: AppColors.danger,
        ),
      );
      return;
    }

    setState(() => _saving = true);
    try {
      final invoice = await ref
          .read(invoiceNotifierProvider(widget.clientId).notifier)
          .createInvoice(
            widget.clientId,
            _selectedTripIds.toList(),
            _notesCtrl.text.trim().isEmpty ? null : _notesCtrl.text.trim(),
          );

      if (sendImmediately && mounted) {
        await ref
            .read(invoiceNotifierProvider(widget.clientId).notifier)
            .markSent(invoice.id);
      }

      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        setState(() => _saving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed to create invoice: $e'),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }

  // Determine trip label: selecting 1 trip = Outbound, 2 trips = Round Trip
  String get _selectionSummary {
    if (_selectedTripIds.isEmpty) return 'No trips selected';
    if (_selectedTripIds.length == 1) return '1 trip selected (One-Way)';
    if (_selectedTripIds.length == 2) return '2 trips selected (Round Trip)';
    return '${_selectedTripIds.length} trips selected';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.close_rounded),
          color: AppColors.textPrimary,
          onPressed: () => Navigator.of(context).pop(),
        ),
        title: Text(
          _step == 0 ? 'Select Trips' : 'Review Invoice',
          style: const TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: Color(0xFF111827),
          ),
        ),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(height: 1, color: const Color(0xFFE5E7EB)),
        ),
      ),
      body: _step == 0 ? _buildTripSelectionStep() : _buildReviewStep(),
      bottomNavigationBar: _buildBottomBar(),
    );
  }

  Widget _buildTripSelectionStep() {
    if (_loadingTrips) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_loadError != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.error_outline_rounded,
              size: 48,
              color: AppColors.brandGray,
            ),
            const SizedBox(height: 12),
            Text(
              _loadError!,
              style: const TextStyle(color: AppColors.brandGray),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _fetchTrips,
              child: const Text('Retry'),
            ),
          ],
        ),
      );
    }

    if (_availableTrips.isEmpty) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.receipt_long_outlined,
              size: 48,
              color: AppColors.brandGray,
            ),
            const SizedBox(height: 12),
            const Text(
              'No completed trips ready for billing.',
              style: TextStyle(color: AppColors.brandGray, fontSize: 15),
            ),
            const SizedBox(height: 6),
            const Text(
              'Trips appear here once they are completed and not yet invoiced.',
              style: TextStyle(color: AppColors.brandGray, fontSize: 12),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
    }

    return Column(
      children: [
        Container(
          width: double.infinity,
          color: const Color(0xFFF9FAFB),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 10),
          child: Text(
            _selectionSummary,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: _selectedTripIds.isEmpty
                  ? AppColors.brandGray
                  : AppColors.primary,
            ),
          ),
        ),
        const Divider(height: 1, color: Color(0xFFE5E7EB)),
        Expanded(
          child: ListView.builder(
            padding: const EdgeInsets.all(20),
            itemCount: _availableTrips.length,
            itemBuilder: (_, i) {
              final trip = _availableTrips[i];
              return BillingReadyTripCard(
                trip: trip,
                isSelected: _selectedTripIds.contains(trip.tripId),
                onSelectionChanged: (selected) {
                  setState(() {
                    if (selected) {
                      _selectedTripIds.add(trip.tripId);
                    } else {
                      _selectedTripIds.remove(trip.tripId);
                    }
                  });
                },
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildReviewStep() {
    final dateFmt = DateFormat('EEE, MMM d · h:mm a');
    final selectedTrips = _availableTrips
        .where((t) => _selectedTripIds.contains(t.tripId))
        .toList();

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Selected trips summary
          Container(
            decoration: BoxDecoration(
              color: Colors.white,
              border: Border.all(color: const Color(0xFFE5E7EB)),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Padding(
                  padding: EdgeInsets.fromLTRB(16, 14, 16, 8),
                  child: Text(
                    'TRIPS INCLUDED',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: AppColors.brandGray,
                      letterSpacing: 0.6,
                    ),
                  ),
                ),
                const Divider(height: 1, color: Color(0xFFF3F4F6)),
                ...selectedTrips.map(
                  (trip) => Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 10,
                    ),
                    child: Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.primary.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            trip.direction ?? 'Unknown',
                            style: const TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w600,
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            dateFmt.format(trip.scheduledAt.toLocal()),
                            style: const TextStyle(
                              fontSize: 13,
                              color: Color(0xFF374151),
                            ),
                          ),
                        ),
                        if (trip.passengerNames.isNotEmpty)
                          Text(
                            '${trip.passengerNames.length} pax',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.brandGray,
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          // Notes field
          Container(
            decoration: BoxDecoration(
              color: Colors.white,
              border: Border.all(color: const Color(0xFFE5E7EB)),
              borderRadius: BorderRadius.circular(12),
            ),
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'NOTES (OPTIONAL)',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: AppColors.brandGray,
                    letterSpacing: 0.6,
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: _notesCtrl,
                  maxLines: 4,
                  decoration: InputDecoration(
                    hintText:
                        'Add notes that will appear on the invoice…',
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                    isDense: true,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          // Info banner
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFFEFF6FF),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFBFDBFE)),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                Icon(
                  Icons.info_outline_rounded,
                  size: 16,
                  color: Color(0xFF2563EB),
                ),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Line items and rates are calculated automatically '
                    'from the client\'s billing rates on the server. '
                    'Save as Draft to review before sending.',
                    style: TextStyle(
                      fontSize: 12,
                      color: Color(0xFF1E40AF),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomBar() {
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
        ),
        child: Row(
          children: [
            if (_step == 1) ...[
              TextButton(
                onPressed: _saving ? null : () => setState(() => _step = 0),
                child: const Text('Back'),
              ),
              const SizedBox(width: 8),
            ],
            const Spacer(),
            if (_step == 0) ...[
              FilledButton(
                onPressed: _selectedTripIds.isEmpty
                    ? null
                    : () => setState(() => _step = 1),
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                ),
                child: const Text('Review'),
              ),
            ] else ...[
              OutlinedButton(
                onPressed: _saving ? null : () => _submit(sendImmediately: false),
                child: const Text('Save Draft'),
              ),
              const SizedBox(width: 8),
              FilledButton(
                onPressed: _saving ? null : () => _submit(sendImmediately: true),
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                ),
                child: _saving
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text('Create & Send'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
