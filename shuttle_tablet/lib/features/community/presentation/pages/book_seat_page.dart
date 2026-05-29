import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_passenger.dart';
import '../../../trips/domain/repositories/i_trip_repository.dart';
import '../../../trips/domain/usecases/add_passenger_usecase.dart';
import '../../../trips/presentation/providers/trips_provider.dart';

class BookSeatPage extends ConsumerStatefulWidget {
  final Trip trip;
  const BookSeatPage({super.key, required this.trip});

  @override
  ConsumerState<BookSeatPage> createState() => _BookSeatPageState();
}

class _BookSeatPageState extends ConsumerState<BookSeatPage> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _contactController = TextEditingController();
  final _seatController = TextEditingController();
  PassengerPaymentStatus _paymentStatus = PassengerPaymentStatus.pending;
  bool _saving = false;

  @override
  void dispose() {
    _nameController.dispose();
    _contactController.dispose();
    _seatController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);

    final result = await sl<AddPassengerUseCase>()(AddPassengerParams(
      tripId: widget.trip.id,
      name: _nameController.text.trim(),
      contactInfo: _contactController.text.trim().isEmpty
          ? null
          : _contactController.text.trim(),
      seatNumber: _seatController.text.trim().isEmpty
          ? null
          : int.tryParse(_seatController.text.trim()),
      paymentStatus: _paymentStatus,
    ));

    if (!mounted) return;
    setState(() => _saving = false);

    result.fold(
      (failure) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(failure.message),
          backgroundColor: AppColors.danger,
        ),
      ),
      (_) {
        ref.invalidate(tripsProvider);
        Navigator.of(context).pop();
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final trip = widget.trip;
    final booked = trip.passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .length;
    final capacity = trip.seatCapacity ?? 0;
    final remaining = capacity - booked;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(height: 1, color: const Color(0xFFE5E7EB)),
        ),
        leading: IconButton(
          onPressed: () => Navigator.of(context).pop(),
          icon: const Icon(Icons.arrow_back_rounded),
          color: const Color(0xFF111827),
        ),
        title: const Text(
          'Book a Seat',
          style: TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w700,
            color: Color(0xFF111827),
          ),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _TripSummaryCard(
              trip: trip,
              booked: booked,
              capacity: capacity,
              remaining: remaining,
            ),
            const SizedBox(height: 24),
            const Text(
              'Passenger Details',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827),
              ),
            ),
            const SizedBox(height: 16),
            Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _FormField(
                    label: 'Passenger Name',
                    required: true,
                    child: TextFormField(
                      controller: _nameController,
                      textCapitalization: TextCapitalization.words,
                      decoration: _inputDecoration('Full name'),
                      validator: (v) =>
                          (v == null || v.trim().isEmpty) ? 'Name is required' : null,
                    ),
                  ),
                  const SizedBox(height: 16),
                  _FormField(
                    label: 'Contact Info',
                    child: TextFormField(
                      controller: _contactController,
                      keyboardType: TextInputType.phone,
                      decoration: _inputDecoration('Phone or email (optional)'),
                    ),
                  ),
                  const SizedBox(height: 16),
                  _FormField(
                    label: 'Seat Number',
                    child: TextFormField(
                      controller: _seatController,
                      keyboardType: TextInputType.number,
                      inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                      decoration: _inputDecoration('Auto-assigned if blank'),
                      validator: (v) {
                        if (v == null || v.trim().isEmpty) return null;
                        final n = int.tryParse(v.trim());
                        if (n == null || n < 1) return 'Enter a valid seat number';
                        if (n > capacity) return 'Seat number exceeds capacity';
                        return null;
                      },
                    ),
                  ),
                  const SizedBox(height: 16),
                  _FormField(
                    label: 'Payment Status',
                    child: SegmentedButton<PassengerPaymentStatus>(
                      selected: {_paymentStatus},
                      onSelectionChanged: (s) =>
                          setState(() => _paymentStatus = s.first),
                      style: SegmentedButton.styleFrom(
                        selectedBackgroundColor:
                            const Color(0xFF0F766E).withValues(alpha: 0.1),
                        selectedForegroundColor: const Color(0xFF0F766E),
                      ),
                      segments: const [
                        ButtonSegment(
                          value: PassengerPaymentStatus.pending,
                          icon: Icon(Icons.pending_outlined, size: 16),
                          label: Text('Pending'),
                        ),
                        ButtonSegment(
                          value: PassengerPaymentStatus.paid,
                          icon: Icon(Icons.check_circle_outline_rounded, size: 16),
                          label: Text('Paid'),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 32),
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: ElevatedButton.icon(
                      onPressed: _saving ? null : _confirm,
                      icon: _saving
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Icons.check_rounded, size: 20),
                      label: Text(_saving ? 'Saving…' : 'Confirm Booking'),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF0F766E),
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                        ),
                        elevation: 0,
                        textStyle: const TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(String hint) => InputDecoration(
        hintText: hint,
        hintStyle: const TextStyle(color: Color(0xFF9CA3AF), fontSize: 14),
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFF0F766E), width: 1.5),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: AppColors.danger),
        ),
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      );
}

class _FormField extends StatelessWidget {
  final String label;
  final bool required;
  final Widget child;

  const _FormField({
    required this.label,
    this.required = false,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        RichText(
          text: TextSpan(
            text: label,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: Color(0xFF374151),
            ),
            children: required
                ? const [
                    TextSpan(
                      text: ' *',
                      style: TextStyle(color: AppColors.danger),
                    ),
                  ]
                : [],
          ),
        ),
        const SizedBox(height: 6),
        child,
      ],
    );
  }
}

class _TripSummaryCard extends StatelessWidget {
  final Trip trip;
  final int booked;
  final int capacity;
  final int remaining;

  const _TripSummaryCard({
    required this.trip,
    required this.booked,
    required this.capacity,
    required this.remaining,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFF0F766E).withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFF0F766E).withValues(alpha: 0.2)),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 32,
                height: 32,
                decoration: BoxDecoration(
                  color: const Color(0xFF0F766E).withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Icon(Icons.route_rounded,
                    size: 16, color: Color(0xFF0F766E)),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${trip.firstStopLocation ?? 'Start'} → ${trip.lastStopLocation ?? 'End'}',
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 16,
            runSpacing: 8,
            children: [
              _SummaryItem(
                icon: Icons.schedule_rounded,
                label: _formatDateTime(trip.scheduledAt),
              ),
              _SummaryItem(
                icon: Icons.event_seat_rounded,
                label: '$remaining of $capacity seats left',
                color: remaining < 5
                    ? const Color(0xFFD97706)
                    : const Color(0xFF0F766E),
              ),
              if (trip.pricePerSeat != null)
                _SummaryItem(
                  icon: Icons.payments_outlined,
                  label: 'TTD ${trip.pricePerSeat!.toStringAsFixed(2)} / seat',
                  color: const Color(0xFF7C3AED),
                ),
            ],
          ),
        ],
      ),
    );
  }

  String _formatDateTime(DateTime dt) {
    final d = dt.toLocal();
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final h = d.hour % 12 == 0 ? 12 : d.hour % 12;
    final m = d.minute.toString().padLeft(2, '0');
    final ampm = d.hour < 12 ? 'AM' : 'PM';
    return '${months[d.month - 1]} ${d.day}, $h:$m $ampm';
  }
}

class _SummaryItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;

  const _SummaryItem({
    required this.icon,
    required this.label,
    this.color = AppColors.brandGray,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(
            fontSize: 13,
            color: color,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}
