import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/community_booking.dart';
import '../../domain/usecases/get_booking_usecase.dart';

class MyBookingsPage extends ConsumerStatefulWidget {
  const MyBookingsPage({super.key});

  @override
  ConsumerState<MyBookingsPage> createState() => _MyBookingsPageState();
}

class _MyBookingsPageState extends ConsumerState<MyBookingsPage> {
  final _refController = TextEditingController();
  final List<CommunityBooking> _bookings = [];
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _refController.dispose();
    super.dispose();
  }

  Future<void> _lookup() async {
    final ref = _refController.text.trim().toUpperCase();
    if (ref.isEmpty) return;

    final already = _bookings.any((b) =>
        b.bookingReference.toUpperCase() == ref);
    if (already) {
      setState(() => _error = 'This booking is already shown below.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    final result = await sl<GetBookingUseCase>()(ref);
    if (!mounted) return;

    result.fold(
      (failure) => setState(() {
        _loading = false;
        _error = failure.message;
      }),
      (booking) => setState(() {
        _loading = false;
        _bookings.insert(0, booking);
        _refController.clear();
      }),
    );
  }

  @override
  Widget build(BuildContext context) {
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
        title: const Text('My Bookings',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            )),
      ),
      body: Column(
        children: [
          Container(
            color: Colors.white,
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Look Up a Booking',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF374151),
                    )),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _refController,
                        textCapitalization: TextCapitalization.characters,
                        decoration: InputDecoration(
                          hintText: 'e.g. NL-AB34',
                          hintStyle: const TextStyle(
                              color: Color(0xFF9CA3AF), fontSize: 14),
                          isDense: true,
                          filled: true,
                          fillColor: const Color(0xFFF9FAFB),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide:
                                const BorderSide(color: Color(0xFFE5E7EB)),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide:
                                const BorderSide(color: Color(0xFFE5E7EB)),
                          ),
                          focusedBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(10),
                            borderSide: const BorderSide(
                                color: Color(0xFF0F766E), width: 1.5),
                          ),
                          contentPadding: const EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                        ),
                        onSubmitted: (_) => _lookup(),
                      ),
                    ),
                    const SizedBox(width: 10),
                    SizedBox(
                      height: 44,
                      child: ElevatedButton(
                        onPressed: _loading ? null : _lookup,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF0F766E),
                          foregroundColor: Colors.white,
                          elevation: 0,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(10),
                          ),
                        ),
                        child: _loading
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Text('Look Up'),
                      ),
                    ),
                  ],
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!,
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.danger)),
                ],
              ],
            ),
          ),
          Expanded(
            child: _bookings.isEmpty
                ? const Center(
                    child: Text('Enter a booking reference above to look up your booking.',
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          color: Color(0xFF9CA3AF),
                          fontSize: 14,
                        )))
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _bookings.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (context, i) =>
                        _BookingCard(booking: _bookings[i]),
                  ),
          ),
          Container(
            color: Colors.white,
            padding:
                const EdgeInsets.symmetric(vertical: 12, horizontal: 20),
            child: const Row(
              children: [
                Icon(Icons.store_outlined,
                    size: 16, color: Color(0xFF6B7280)),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Cash payments: Northern Link booth, Leaf Rapids Mall, every Thursday.',
                    style:
                        TextStyle(fontSize: 12, color: Color(0xFF6B7280)),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _BookingCard extends StatelessWidget {
  final CommunityBooking booking;
  const _BookingCard({required this.booking});

  @override
  Widget build(BuildContext context) {
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final dep = booking.departureDate.toLocal();
    final depStr =
        '${months[dep.month - 1]} ${dep.day}, ${dep.year}';

    final cutoffLocal = booking.cutoffDeadline?.toLocal();
    final cutoffStr = cutoffLocal != null
        ? 'Thursday ${months[cutoffLocal.month - 1]} ${cutoffLocal.day} at 6:00 PM CT'
        : null;

    final (statusLabel, statusColor) = _statusStyle(booking.status);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(booking.bookingReference,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.5,
                    color: Color(0xFF0F766E),
                  )),
              const Spacer(),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                  border:
                      Border.all(color: statusColor.withValues(alpha: 0.3)),
                ),
                child: Text(statusLabel,
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: statusColor,
                    )),
              ),
            ],
          ),
          const SizedBox(height: 10),
          _Row(icon: Icons.route_rounded, text: booking.route),
          _Row(
              icon: Icons.calendar_today_rounded,
              text: depStr),
          _Row(
              icon: Icons.payments_outlined,
              text: '\$${booking.fare.toStringAsFixed(0)} — '
                  '${booking.tripType == TripType.returnTrip ? 'Return' : 'One Way'}'),
          if ((booking.status == 'Tentative' ||
                  booking.status == 'AwaitingPayment') &&
              cutoffStr != null) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: booking.status == 'AwaitingPayment'
                    ? const Color(0xFFFFF7ED)
                    : const Color(0xFFF9FAFB),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(
                    color: booking.status == 'AwaitingPayment'
                        ? const Color(0xFFFED7AA)
                        : const Color(0xFFE5E7EB)),
              ),
              child: Row(
                children: [
                  Icon(Icons.schedule_rounded,
                      size: 14,
                      color: booking.status == 'AwaitingPayment'
                          ? const Color(0xFFEA580C)
                          : const Color(0xFF6B7280)),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      booking.status == 'AwaitingPayment'
                          ? 'Payment due: $cutoffStr. Pay cash at the Northern Link booth, Leaf Rapids Mall.'
                          : 'Payment deadline: $cutoffStr.',
                      style: TextStyle(
                        fontSize: 11,
                        color: booking.status == 'AwaitingPayment'
                            ? const Color(0xFF92400E)
                            : const Color(0xFF6B7280),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  (String, Color) _statusStyle(String status) {
    return switch (status) {
      'Confirmed' => ('CONFIRMED', const Color(0xFF059669)),
      'AwaitingPayment' => ('PAYMENT DUE', const Color(0xFFEA580C)),
      'Released' => ('RELEASED', AppColors.danger),
      'Cancelled' => ('CANCELLED', AppColors.brandGray),
      _ => ('TENTATIVE', const Color(0xFFD97706)),
    };
  }
}

class _Row extends StatelessWidget {
  final IconData icon;
  final String text;
  const _Row({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        children: [
          Icon(icon, size: 13, color: const Color(0xFF9CA3AF)),
          const SizedBox(width: 6),
          Expanded(
            child: Text(text,
                style: const TextStyle(
                    fontSize: 13, color: Color(0xFF374151))),
          ),
        ],
      ),
    );
  }
}
