import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/calendar_day.dart';
import '../providers/calendar_provider.dart';
import '../widgets/day_detail_bottom_sheet.dart';

class AdminCalendarPage extends ConsumerWidget {
  const AdminCalendarPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final calendarAsync = ref.watch(adminCalendarProvider);

    return calendarAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('$e'),
            TextButton(
              onPressed: () => ref.invalidate(adminCalendarProvider),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (days) {
        final zone1 = days.where((d) => !d.isZone2).toList();
        final zone2 = days.where((d) => d.isZone2).toList();

        return RefreshIndicator(
          onRefresh: () => ref.read(adminCalendarProvider.notifier).refresh(),
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _zoneSectionTitle('Week 1 — Zone 1'),
                const SizedBox(height: 10),
                _AdminCalendarGrid(days: zone1),
                const SizedBox(height: 24),
                _zoneSectionTitle('Week 2 — Zone 2'),
                const SizedBox(height: 10),
                _AdminCalendarGrid(days: zone2, zone2: true),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _zoneSectionTitle(String text) {
    return Text(text,
        style: const TextStyle(
          fontSize: 15,
          fontWeight: FontWeight.w700,
          color: Color(0xFF111827),
        ));
  }
}

class _AdminCalendarGrid extends StatelessWidget {
  final List<CalendarDay> days;
  final bool zone2;

  const _AdminCalendarGrid({required this.days, this.zone2 = false});

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 7,
        mainAxisSpacing: 6,
        crossAxisSpacing: 6,
        childAspectRatio: 0.60,
      ),
      itemCount: days.length,
      itemBuilder: (context, i) => _AdminCalendarCell(
        day: days[i],
        zone2: zone2,
      ),
    );
  }
}

class _AdminCalendarCell extends ConsumerWidget {
  final CalendarDay day;
  final bool zone2;
  const _AdminCalendarCell({required this.day, this.zone2 = false});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unavailable = day.status == CalendarDayStatus.unavailable;
    final (bg, accent) = _colors();
    final dow = ['M', 'T', 'W', 'T', 'F', 'S', 'S'][day.date.weekday - 1];

    return GestureDetector(
      onTap: day.date.dayOfWeek == 'Sunday'
          ? null
          : () => showDayDetailSheet(context, ref, day),
      child: Container(
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(
            color: day.isBlocked
                ? AppColors.danger.withValues(alpha: 0.4)
                : unavailable
                    ? const Color(0xFFE5E7EB)
                    : accent.withValues(alpha: zone2 ? 0.4 : 0.6),
          ),
        ),
        padding: const EdgeInsets.symmetric(vertical: 5, horizontal: 3),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(dow,
                style: TextStyle(
                  fontSize: 9,
                  fontWeight: FontWeight.w500,
                  color: unavailable
                      ? const Color(0xFFD1D5DB)
                      : const Color(0xFF6B7280),
                )),
            const SizedBox(height: 2),
            Text('${day.date.day}',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  color: unavailable
                      ? const Color(0xFFD1D5DB)
                      : const Color(0xFF111827),
                )),
            const SizedBox(height: 3),
            if (!unavailable && !day.isBlocked) ...[
              _CountChip(
                  count: day.confirmedCount,
                  color: const Color(0xFF059669),
                  icon: '✓'),
              const SizedBox(height: 2),
              _CountChip(
                  count: day.tentativeCount,
                  color: const Color(0xFFD97706),
                  icon: '?'),
            ],
            if (day.isBlocked)
              const Icon(Icons.block_rounded,
                  size: 14, color: AppColors.danger),
            if (unavailable && !day.isBlocked)
              const SizedBox(height: 14),
          ],
        ),
      ),
    );
  }

  (Color, Color) _colors() {
    if (day.isBlocked) {
      return (AppColors.danger.withValues(alpha: 0.05), AppColors.danger);
    }
    return switch (day.status) {
      CalendarDayStatus.go => (
          const Color(0xFFDCFCE7),
          const Color(0xFF15803D)
        ),
      CalendarDayStatus.building => (
          const Color(0xFFFFF7ED),
          const Color(0xFFEA580C)
        ),
      CalendarDayStatus.open => (Colors.white, const Color(0xFFE5E7EB)),
      CalendarDayStatus.unavailable => (
          const Color(0xFFF9FAFB),
          const Color(0xFFE5E7EB)
        ),
    };
  }
}

class _CountChip extends StatelessWidget {
  final int count;
  final Color color;
  final String icon;

  const _CountChip({
    required this.count,
    required this.color,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(icon,
            style: TextStyle(fontSize: 8, color: color)),
        const SizedBox(width: 2),
        Text('$count',
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: color,
            )),
      ],
    );
  }
}

extension on DateTime {
  String get dayOfWeek {
    const days = [
      'Monday', 'Tuesday', 'Wednesday', 'Thursday',
      'Friday', 'Saturday', 'Sunday'
    ];
    return days[weekday - 1];
  }
}
