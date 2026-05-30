import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/calendar_day.dart';
import '../../domain/entities/community_booking.dart';
import '../providers/booking_flow_provider.dart';
import '../providers/calendar_provider.dart';

class BookingFlowPage extends ConsumerStatefulWidget {
  const BookingFlowPage({super.key});

  @override
  ConsumerState<BookingFlowPage> createState() => _BookingFlowPageState();
}

class _BookingFlowPageState extends ConsumerState<BookingFlowPage> {
  final _pageController = PageController();
  int _currentStep = 0;

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _nextPage() {
    setState(() => _currentStep++);
    _pageController.nextPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  void _prevPage() {
    setState(() => _currentStep--);
    _pageController.previousPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: _currentStep < 3
          ? AppBar(
              backgroundColor: Colors.white,
              surfaceTintColor: Colors.transparent,
              elevation: 0,
              bottom: PreferredSize(
                preferredSize: const Size.fromHeight(1),
                child: Container(height: 1, color: const Color(0xFFE5E7EB)),
              ),
              leading: _currentStep > 0
                  ? IconButton(
                      icon: const Icon(Icons.arrow_back_rounded),
                      color: const Color(0xFF111827),
                      onPressed: _prevPage,
                    )
                  : null,
              title: Text(
                ['Select Route', 'Choose Date', 'Your Details'][_currentStep],
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                ),
              ),
              actions: [
                Padding(
                  padding: const EdgeInsets.only(right: 16),
                  child: _StepIndicator(current: _currentStep, total: 3),
                ),
              ],
            )
          : null,
      body: PageView(
        controller: _pageController,
        physics: const NeverScrollableScrollPhysics(),
        children: [
          RouteSelectionStep(onNext: _nextPage),
          DateSelectionStep(onNext: _nextPage),
          PassengerDetailsStep(onNext: _nextPage),
          BookingConfirmationStep(
            onBookAnother: () {
              ref.read(bookingFlowProvider.notifier).reset();
              setState(() => _currentStep = 0);
              _pageController.jumpToPage(0);
            },
          ),
        ],
      ),
    );
  }
}

class _StepIndicator extends StatelessWidget {
  final int current;
  final int total;
  const _StepIndicator({required this.current, required this.total});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(total, (i) {
        final active = i == current;
        final done = i < current;
        return AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          margin: const EdgeInsets.symmetric(horizontal: 3),
          width: active ? 20 : 8,
          height: 8,
          decoration: BoxDecoration(
            color: done || active
                ? const Color(0xFF0F766E)
                : const Color(0xFFD1D5DB),
            borderRadius: BorderRadius.circular(4),
          ),
        );
      }),
    );
  }
}

// ─── Step 1: Route Selection ──────────────────────────────────────────────────

class RouteSelectionStep extends ConsumerWidget {
  final VoidCallback onNext;
  const RouteSelectionStep({super.key, required this.onNext});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flow = ref.watch(bookingFlowProvider);

    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _SectionTitle('Direction'),
          const SizedBox(height: 12),
          _DirectionCard(
            label: 'Thompson → Lynn Lake',
            subtitle: 'Outbound departure',
            icon: Icons.arrow_forward_rounded,
            selected: flow.direction == TripDirection.outbound,
            onTap: () => ref
                .read(bookingFlowProvider.notifier)
                .setRoute(TripDirection.outbound,
                    flow.tripType ?? TripType.oneWay),
          ),
          const SizedBox(height: 12),
          _DirectionCard(
            label: 'Lynn Lake → Thompson',
            subtitle: 'Inbound return',
            icon: Icons.arrow_back_rounded,
            selected: flow.direction == TripDirection.inbound,
            onTap: () => ref
                .read(bookingFlowProvider.notifier)
                .setRoute(TripDirection.inbound,
                    flow.tripType ?? TripType.oneWay),
          ),
          const SizedBox(height: 28),
          const _SectionTitle('Trip Type'),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _TripTypeCard(
                  label: 'One Way',
                  price: '\$90',
                  selected: flow.tripType == TripType.oneWay ||
                      flow.tripType == null,
                  onTap: () => ref
                      .read(bookingFlowProvider.notifier)
                      .setRoute(flow.direction ?? TripDirection.outbound,
                          TripType.oneWay),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _TripTypeCard(
                  label: 'Return',
                  price: '\$170',
                  selected: flow.tripType == TripType.returnTrip,
                  onTap: () => ref
                      .read(bookingFlowProvider.notifier)
                      .setRoute(flow.direction ?? TripDirection.outbound,
                          TripType.returnTrip),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          _NoticeBox(
            icon: Icons.info_outline_rounded,
            text:
                'No payment is collected at booking. Your seat is held tentatively until Thursday 6:00 PM CT.',
          ),
          const SizedBox(height: 32),
          SizedBox(
            width: double.infinity,
            height: 52,
            child: ElevatedButton(
              onPressed: flow.direction != null ? onNext : null,
              style: _primaryButtonStyle(),
              child: const Text('Next: Choose a Date',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
            ),
          ),
        ],
      ),
    );
  }
}

class _DirectionCard extends StatelessWidget {
  final String label;
  final String subtitle;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  const _DirectionCard({
    required this.label,
    required this.subtitle,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: selected
              ? const Color(0xFF0F766E).withValues(alpha: 0.06)
              : Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: selected
                ? const Color(0xFF0F766E)
                : const Color(0xFFE5E7EB),
            width: selected ? 2 : 1,
          ),
        ),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: selected
                    ? const Color(0xFF0F766E).withValues(alpha: 0.12)
                    : const Color(0xFFF3F4F6),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon,
                  size: 20,
                  color: selected
                      ? const Color(0xFF0F766E)
                      : const Color(0xFF6B7280)),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(label,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color: selected
                            ? const Color(0xFF0F766E)
                            : const Color(0xFF111827),
                      )),
                  Text(subtitle,
                      style: const TextStyle(
                          fontSize: 13, color: Color(0xFF6B7280))),
                ],
              ),
            ),
            if (selected)
              const Icon(Icons.check_circle_rounded,
                  color: Color(0xFF0F766E), size: 22),
          ],
        ),
      ),
    );
  }
}

class _TripTypeCard extends StatelessWidget {
  final String label;
  final String price;
  final bool selected;
  final VoidCallback onTap;

  const _TripTypeCard({
    required this.label,
    required this.price,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 16),
        decoration: BoxDecoration(
          color: selected
              ? const Color(0xFF0F766E).withValues(alpha: 0.06)
              : Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: selected
                ? const Color(0xFF0F766E)
                : const Color(0xFFE5E7EB),
            width: selected ? 2 : 1,
          ),
        ),
        child: Column(
          children: [
            Text(label,
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: selected
                      ? const Color(0xFF0F766E)
                      : const Color(0xFF374151),
                )),
            const SizedBox(height: 6),
            Text(price,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: selected
                      ? const Color(0xFF0F766E)
                      : const Color(0xFF111827),
                )),
          ],
        ),
      ),
    );
  }
}

// ─── Step 2: Date Selection ───────────────────────────────────────────────────

class DateSelectionStep extends ConsumerWidget {
  final VoidCallback onNext;
  const DateSelectionStep({super.key, required this.onNext});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final calendarAsync = ref.watch(calendarProvider);
    final flow = ref.watch(bookingFlowProvider);

    return calendarAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('Could not load calendar: $e'),
            const SizedBox(height: 12),
            TextButton(
              onPressed: () => ref.invalidate(calendarProvider),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (days) {
        final zone1 = days.where((d) => !d.isZone2).toList();
        final zone2 = days.where((d) => d.isZone2).toList();

        return SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const _SectionTitle('Week 1 — Zone 1'),
              const SizedBox(height: 10),
              _CalendarGrid(
                days: zone1,
                selected: flow.selectedDay,
                onSelect: (day) =>
                    ref.read(bookingFlowProvider.notifier).setDate(day),
              ),
              const SizedBox(height: 20),
              const _SectionTitle('Week 2 — Zone 2'),
              const SizedBox(height: 6),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: const Color(0xFFFFF7ED),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: const Color(0xFFFED7AA)),
                ),
                child: const Text(
                  'Dates in Week 2 are subject to scheduling changes. You will be notified before payment is due if your date becomes unavailable.',
                  style: TextStyle(fontSize: 12, color: Color(0xFF92400E)),
                ),
              ),
              const SizedBox(height: 10),
              _CalendarGrid(
                days: zone2,
                selected: flow.selectedDay,
                onSelect: (day) =>
                    ref.read(bookingFlowProvider.notifier).setDate(day),
                zone2: true,
              ),
              if (flow.selectedDay != null) ...[
                const SizedBox(height: 20),
                _SelectedDaySummary(day: flow.selectedDay!),
              ],
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: ElevatedButton(
                  onPressed: flow.selectedDay != null &&
                          flow.selectedDay!.status != CalendarDayStatus.unavailable
                      ? onNext
                      : null,
                  style: _primaryButtonStyle(),
                  child: const Text('Next: Your Details',
                      style:
                          TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _CalendarGrid extends StatelessWidget {
  final List<CalendarDay> days;
  final CalendarDay? selected;
  final ValueChanged<CalendarDay> onSelect;
  final bool zone2;

  const _CalendarGrid({
    required this.days,
    required this.selected,
    required this.onSelect,
    this.zone2 = false,
  });

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 7,
        mainAxisSpacing: 6,
        crossAxisSpacing: 6,
        childAspectRatio: 0.72,
      ),
      itemCount: days.length,
      itemBuilder: (context, i) {
        return _CalendarCell(
          day: days[i],
          isSelected: selected?.date.day == days[i].date.day &&
              selected?.date.month == days[i].date.month,
          zone2: zone2,
          onTap: () {
            if (days[i].status != CalendarDayStatus.unavailable) {
              onSelect(days[i]);
            }
          },
        );
      },
    );
  }
}

class _CalendarCell extends StatelessWidget {
  final CalendarDay day;
  final bool isSelected;
  final bool zone2;
  final VoidCallback onTap;

  const _CalendarCell({
    required this.day,
    required this.isSelected,
    required this.zone2,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final unavailable = day.status == CalendarDayStatus.unavailable;
    final (bg, fg, borderColor) = _colors();
    final daysOfWeek = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
    final dow = daysOfWeek[day.date.weekday - 1];

    return GestureDetector(
      onTap: unavailable ? null : onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 120),
        decoration: BoxDecoration(
          color: isSelected ? const Color(0xFF0F766E) : bg,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: isSelected
                ? const Color(0xFF0F766E)
                : zone2
                    ? borderColor.withValues(alpha: 0.5)
                    : borderColor,
            width: isSelected ? 2 : 1,
            style: zone2 ? BorderStyle.solid : BorderStyle.solid,
          ),
        ),
        padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(dow,
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w500,
                  color: isSelected
                      ? Colors.white
                      : unavailable
                          ? const Color(0xFFD1D5DB)
                          : const Color(0xFF6B7280),
                )),
            const SizedBox(height: 3),
            Text('${day.date.day}',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                  color: isSelected
                      ? Colors.white
                      : unavailable
                          ? const Color(0xFFD1D5DB)
                          : fg,
                )),
            const SizedBox(height: 3),
            _statusBadge(isSelected),
          ],
        ),
      ),
    );
  }

  (Color, Color, Color) _colors() {
    return switch (day.status) {
      CalendarDayStatus.go => (
          const Color(0xFFDCFCE7),
          const Color(0xFF15803D),
          const Color(0xFF86EFAC)
        ),
      CalendarDayStatus.building => (
          const Color(0xFFFFF7ED),
          const Color(0xFFEA580C),
          const Color(0xFFFED7AA)
        ),
      CalendarDayStatus.open => (
          Colors.white,
          const Color(0xFF111827),
          const Color(0xFFE5E7EB)
        ),
      CalendarDayStatus.unavailable => (
          const Color(0xFFF9FAFB),
          const Color(0xFF9CA3AF),
          const Color(0xFFE5E7EB)
        ),
    };
  }

  Widget _statusBadge(bool selected) {
    if (day.status == CalendarDayStatus.unavailable) {
      return const SizedBox(height: 14);
    }
    if (day.status == CalendarDayStatus.go) {
      return Text('GO',
          style: TextStyle(
            fontSize: 9,
            fontWeight: FontWeight.w800,
            color: selected ? Colors.white : const Color(0xFF15803D),
          ));
    }
    if (day.status == CalendarDayStatus.building) {
      final confirmed = day.confirmedCount;
      return Text('$confirmed/2',
          style: TextStyle(
            fontSize: 9,
            fontWeight: FontWeight.w700,
            color: selected ? Colors.white : const Color(0xFFEA580C),
          ));
    }
    return Text('Open',
        style: TextStyle(
          fontSize: 8,
          color: selected ? Colors.white70 : const Color(0xFF9CA3AF),
        ));
  }
}

class _SelectedDaySummary extends StatelessWidget {
  final CalendarDay day;
  const _SelectedDaySummary({required this.day});

  @override
  Widget build(BuildContext context) {
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final dateStr =
        '${day.dayOfWeek}, ${months[day.date.month - 1]} ${day.date.day}';
    final (statusLabel, statusColor) = switch (day.status) {
      CalendarDayStatus.go => ('GO', const Color(0xFF15803D)),
      CalendarDayStatus.building => ('BUILDING', const Color(0xFFEA580C)),
      CalendarDayStatus.open => ('OPEN', const Color(0xFF6B7280)),
      CalendarDayStatus.unavailable => ('UNAVAILABLE', AppColors.danger),
    };

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF0F766E).withValues(alpha: 0.05),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFF0F766E).withValues(alpha: 0.2)),
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(dateStr,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827),
                    )),
                const SizedBox(height: 4),
                Text(
                  '${day.confirmedCount} confirmed · ${day.tentativeCount} tentative · ${day.availableSeats} seats left',
                  style: const TextStyle(
                      fontSize: 12, color: Color(0xFF6B7280)),
                ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(8),
              border:
                  Border.all(color: statusColor.withValues(alpha: 0.3)),
            ),
            child: Text(statusLabel,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w700,
                  color: statusColor,
                )),
          ),
        ],
      ),
    );
  }
}

// ─── Step 3: Passenger Details ────────────────────────────────────────────────

class PassengerDetailsStep extends ConsumerStatefulWidget {
  final VoidCallback onNext;
  const PassengerDetailsStep({super.key, required this.onNext});

  @override
  ConsumerState<PassengerDetailsStep> createState() =>
      _PassengerDetailsStepState();
}

class _PassengerDetailsStepState
    extends ConsumerState<PassengerDetailsStep> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  bool _submitting = false;

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);

    ref.read(bookingFlowProvider.notifier).setPassengerDetails(
          _nameController.text.trim(),
          _phoneController.text.replaceAll(RegExp(r'\D'), ''),
          _emailController.text.trim(),
        );

    final ok = await ref.read(bookingFlowProvider.notifier).submitBooking();
    if (!mounted) return;
    setState(() => _submitting = false);

    if (ok) {
      widget.onNext();
    }
  }

  @override
  Widget build(BuildContext context) {
    final flow = ref.watch(bookingFlowProvider);

    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(
          24, 24, 24, 24 + MediaQuery.viewInsetsOf(context).bottom),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const _SectionTitle('Passenger Information'),
            const SizedBox(height: 16),
            _FieldLabel('Full Name', required: true),
            const SizedBox(height: 6),
            TextFormField(
              controller: _nameController,
              textCapitalization: TextCapitalization.words,
              decoration: _inputDec('Your full name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Name is required' : null,
            ),
            const SizedBox(height: 16),
            _FieldLabel('Phone Number', required: true),
            const SizedBox(height: 6),
            TextFormField(
              controller: _phoneController,
              keyboardType: TextInputType.phone,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp(r'[\d\s\-]'))
              ],
              decoration: _inputDec('10-digit Canadian number'),
              validator: (v) {
                final digits = v?.replaceAll(RegExp(r'\D'), '') ?? '';
                if (digits.isEmpty) return 'Phone number is required';
                if (digits.length != 10)
                  return 'Enter a 10-digit Canadian number';
                return null;
              },
            ),
            const SizedBox(height: 16),
            _FieldLabel('Email Address', required: true),
            const SizedBox(height: 6),
            TextFormField(
              controller: _emailController,
              keyboardType: TextInputType.emailAddress,
              decoration: _inputDec('your@email.com'),
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Email is required';
                if (!RegExp(r'^[^@]+@[^@]+\.[^@]+$').hasMatch(v.trim())) {
                  return 'Enter a valid email address';
                }
                return null;
              },
            ),
            const SizedBox(height: 20),
            _NoticeBox(
              icon: Icons.payments_outlined,
              text:
                  'Cash payments accepted at the Northern Link booth, Leaf Rapids Mall, every Thursday.',
            ),
            const SizedBox(height: 10),
            _NoticeBox(
              icon: Icons.schedule_rounded,
              text:
                  'No payment is collected today. A payment request will be sent to your phone and email before Thursday at 6:00 PM CT. Unpaid seats are released at the deadline.',
            ),
            if (flow.error != null) ...[
              const SizedBox(height: 12),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppColors.danger.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                      color: AppColors.danger.withValues(alpha: 0.3)),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.error_outline,
                        color: AppColors.danger, size: 18),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(flow.error!,
                          style: const TextStyle(
                              color: AppColors.danger, fontSize: 13)),
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 28),
            SizedBox(
              width: double.infinity,
              height: 52,
              child: ElevatedButton(
                onPressed: _submitting ? null : _submit,
                style: _primaryButtonStyle(),
                child: _submitting
                    ? const SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : const Text('Hold My Seat',
                        style: TextStyle(
                            fontSize: 16, fontWeight: FontWeight.w600)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─── Step 4: Booking Confirmation ─────────────────────────────────────────────

class BookingConfirmationStep extends ConsumerWidget {
  final VoidCallback onBookAnother;
  const BookingConfirmationStep({super.key, required this.onBookAnother});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final flow = ref.watch(bookingFlowProvider);
    final booking = flow.result;

    if (booking == null) {
      return const Center(child: CircularProgressIndicator());
    }

    final cutoffLocal = booking.cutoffDeadline?.toLocal();
    final months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    final cutoffStr = cutoffLocal != null
        ? 'Thursday ${months[cutoffLocal.month - 1]} ${cutoffLocal.day} at 6:00 PM CT'
        : 'Thursday 6:00 PM CT';

    final depDate = booking.departureDate.toLocal();
    final weekdays = [
      'Monday', 'Tuesday', 'Wednesday', 'Thursday',
      'Friday', 'Saturday', 'Sunday'
    ];
    final depStr =
        '${weekdays[depDate.weekday - 1]}, ${months[depDate.month - 1]} ${depDate.day}';

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              const SizedBox(height: 16),
              Container(
                width: 64,
                height: 64,
                decoration: BoxDecoration(
                  color: const Color(0xFF0F766E).withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(32),
                ),
                child: const Icon(Icons.check_rounded,
                    size: 36, color: Color(0xFF0F766E)),
              ),
              const SizedBox(height: 16),
              const Text('Seat Held!',
                  style: TextStyle(
                    fontSize: 24,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF111827),
                  )),
              const SizedBox(height: 24),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: const Color(0xFFE5E7EB)),
                ),
                child: Column(
                  children: [
                    Text(booking.bookingReference,
                        style: const TextStyle(
                          fontSize: 32,
                          fontWeight: FontWeight.w900,
                          letterSpacing: 3,
                          color: Color(0xFF0F766E),
                        )),
                    const SizedBox(height: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 12, vertical: 4),
                      decoration: BoxDecoration(
                        color: const Color(0xFFFFF7ED),
                        borderRadius: BorderRadius.circular(8),
                        border:
                            Border.all(color: const Color(0xFFFED7AA)),
                      ),
                      child: const Text('TENTATIVE',
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                            color: Color(0xFFEA580C),
                          )),
                    ),
                    const SizedBox(height: 20),
                    _BookingRow(label: 'Route', value: booking.route),
                    _BookingRow(label: 'Date', value: depStr),
                    _BookingRow(
                        label: 'Type',
                        value: booking.tripType == TripType.returnTrip
                            ? 'Return'
                            : 'One Way'),
                    _BookingRow(
                        label: 'Fare',
                        value:
                            '\$${booking.fare.toStringAsFixed(0)}'),
                    _BookingRow(
                        label: 'Payment Deadline', value: cutoffStr),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: const Color(0xFFF0FDF4),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFFBBF7D0)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('What happens next',
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFF14532D),
                        )),
                    const SizedBox(height: 10),
                    _NextStep(
                        n: '1',
                        text:
                            'You will receive a confirmation SMS and email.'),
                    _NextStep(
                        n: '2',
                        text:
                            'On Thursday, a payment request will be sent to your phone and email.'),
                    _NextStep(
                        n: '3',
                        text:
                            'Pay cash at the Northern Link booth, Leaf Rapids Mall, every Thursday.'),
                    _NextStep(
                        n: '4',
                        text:
                            'Once your payment is confirmed, your status changes to CONFIRMED and the trip is on!'),
                  ],
                ),
              ),
              const SizedBox(height: 28),
              SizedBox(
                width: double.infinity,
                height: 52,
                child: OutlinedButton(
                  onPressed: onBookAnother,
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Color(0xFF0F766E), width: 1.5),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                  ),
                  child: const Text('Book Another Seat',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: Color(0xFF0F766E),
                      )),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BookingRow extends StatelessWidget {
  final String label;
  final String value;
  const _BookingRow({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label,
              style: const TextStyle(
                  fontSize: 13, color: Color(0xFF6B7280))),
          Flexible(
            child: Text(value,
                textAlign: TextAlign.right,
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: Color(0xFF111827),
                )),
          ),
        ],
      ),
    );
  }
}

class _NextStep extends StatelessWidget {
  final String n;
  final String text;
  const _NextStep({required this.n, required this.text});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 20,
            height: 20,
            margin: const EdgeInsets.only(top: 1, right: 10),
            decoration: BoxDecoration(
              color: const Color(0xFF14532D),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Center(
              child: Text(n,
                  style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w700,
                      color: Colors.white)),
            ),
          ),
          Expanded(
            child: Text(text,
                style: const TextStyle(
                    fontSize: 13, color: Color(0xFF166534))),
          ),
        ],
      ),
    );
  }
}

// ─── Shared Widgets ───────────────────────────────────────────────────────────

class _SectionTitle extends StatelessWidget {
  final String text;
  const _SectionTitle(this.text);

  @override
  Widget build(BuildContext context) {
    return Text(text,
        style: const TextStyle(
          fontSize: 16,
          fontWeight: FontWeight.w700,
          color: Color(0xFF111827),
        ));
  }
}

class _FieldLabel extends StatelessWidget {
  final String text;
  final bool required;
  const _FieldLabel(this.text, {this.required = false});

  @override
  Widget build(BuildContext context) {
    return RichText(
      text: TextSpan(
        text: text,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: Color(0xFF374151),
        ),
        children: required
            ? const [
                TextSpan(
                    text: ' *', style: TextStyle(color: AppColors.danger))
              ]
            : [],
      ),
    );
  }
}

class _NoticeBox extends StatelessWidget {
  final IconData icon;
  final String text;
  const _NoticeBox({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFF0F9FF),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: const Color(0xFFBAE6FD)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 16, color: const Color(0xFF0369A1)),
          const SizedBox(width: 8),
          Expanded(
            child: Text(text,
                style: const TextStyle(
                    fontSize: 12, color: Color(0xFF0C4A6E))),
          ),
        ],
      ),
    );
  }
}

InputDecoration _inputDec(String hint) => InputDecoration(
      hintText: hint,
      hintStyle: const TextStyle(color: Color(0xFF9CA3AF), fontSize: 14),
      isDense: true,
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
      contentPadding:
          const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
    );

ButtonStyle _primaryButtonStyle() => ElevatedButton.styleFrom(
      backgroundColor: const Color(0xFF0F766E),
      foregroundColor: Colors.white,
      disabledBackgroundColor: const Color(0xFFD1D5DB),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      elevation: 0,
    );
