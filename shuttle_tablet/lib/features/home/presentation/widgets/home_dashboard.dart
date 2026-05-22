import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';

class HomeDashboard extends StatefulWidget {
  const HomeDashboard({super.key});

  @override
  State<HomeDashboard> createState() => _HomeDashboardState();
}

class _HomeDashboardState extends State<HomeDashboard> {
  bool _alertDismissed = false;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildHeader(),
          const SizedBox(height: 24),
          if (!_alertDismissed) ...[
            _buildAlert(),
            const SizedBox(height: 24),
          ],
          _buildKpiGrid(),
          const SizedBox(height: 32),
          _buildContentGrid(),
        ],
      ),
    );
  }

  // ── Header ────────────────────────────────────────────────────────────────

  Widget _buildHeader() {
    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Overview Dashboard',
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: const Color(0xFF111827),
                    ),
              ),
              const SizedBox(height: 4),
              const Text(
                "Today's operational status across all active trips.",
                style: TextStyle(fontSize: 14, color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
        const SizedBox(width: 16),
        OutlinedButton.icon(
          onPressed: () {},
          icon: const Icon(Icons.calendar_today_rounded, size: 15),
          label: const Text('Today'),
          style: OutlinedButton.styleFrom(
            foregroundColor: const Color(0xFF374151),
            side: const BorderSide(color: Color(0xFFE5E7EB)),
            padding:
                const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12)),
            textStyle: const TextStyle(
                fontSize: 13, fontWeight: FontWeight.w500),
          ),
        ),
        const SizedBox(width: 10),
        ElevatedButton.icon(
          onPressed: () {},
          icon: const Icon(Icons.add_rounded, size: 18),
          label: const Text('New Trip'),
          style: ElevatedButton.styleFrom(
            backgroundColor: AppColors.primary,
            foregroundColor: Colors.white,
            elevation: 0,
            padding:
                const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12)),
            textStyle: const TextStyle(
                fontSize: 13, fontWeight: FontWeight.w500),
          ),
        ),
      ],
    );
  }

  // ── Alert Banner ──────────────────────────────────────────────────────────

  Widget _buildAlert() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
            color: AppColors.warning.withValues(alpha: 0.3)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.warning_amber_rounded,
              color: AppColors.warning, size: 18),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Action Required: Compliance Expiries',
                  style: TextStyle(
                      fontSize: 13, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 4),
                RichText(
                  text: const TextSpan(
                    style: TextStyle(
                        fontSize: 13, color: Color(0xFF374151)),
                    children: [
                      TextSpan(
                          text:
                              '3 drivers have licenses expiring within the next 15 days. '),
                      TextSpan(
                        text: 'Review now',
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w500,
                          decoration: TextDecoration.underline,
                          decorationColor: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          GestureDetector(
            onTap: () => setState(() => _alertDismissed = true),
            child: const Icon(Icons.close_rounded,
                color: Color(0xFF9CA3AF), size: 18),
          ),
        ],
      ),
    );
  }

  // ── KPI Grid ──────────────────────────────────────────────────────────────

  Widget _buildKpiGrid() {
    return LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth > 700;
        final cols = isWide ? 4 : 2;
        return GridView.count(
          crossAxisCount: cols,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          crossAxisSpacing: 20,
          mainAxisSpacing: 20,
          childAspectRatio: isWide ? 0.95 : 1.05,
          children: const [
            _TotalTripsCard(),
            _UnassignedTripsCard(),
            _ActiveDriversCard(),
            _PendingInspectionsCard(),
          ],
        );
      },
    );
  }

  // ── Content Grid ─────────────────────────────────────────────────────────

  Widget _buildContentGrid() {
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth > 700) {
          return Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              Expanded(flex: 2, child: _DispatchCard()),
              SizedBox(width: 24),
              Expanded(flex: 1, child: _QuickActionsCard()),
            ],
          );
        }
        return const Column(
          children: [
            _DispatchCard(),
            SizedBox(height: 24),
            _QuickActionsCard(),
          ],
        );
      },
    );
  }
}

// ─── Shared card decoration helper ───────────────────────────────────────────

BoxDecoration _whiteCardDecoration() => BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(24),
      border: Border.all(color: const Color(0xFFF3F4F6)),
      boxShadow: [
        BoxShadow(
          color: Colors.black.withValues(alpha: 0.08),
          blurRadius: 40,
          offset: const Offset(0, 10),
        ),
      ],
    );

// ─── KPI Cards ───────────────────────────────────────────────────────────────

class _TotalTripsCard extends StatelessWidget {
  const _TotalTripsCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: AppColors.primary,
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.08),
            blurRadius: 40,
            offset: const Offset(0, 10),
          ),
        ],
      ),
      child: Stack(
        children: [
          Positioned(
            top: -10,
            right: -10,
            child: Opacity(
              opacity: 0.12,
              child: const Icon(
                Icons.route_rounded,
                size: 90,
                color: Colors.white,
              ),
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  _Chip(label: 'Active', isLight: true),
                  const SizedBox(width: 6),
                  _Chip(label: 'Today', isLight: true),
                  const Spacer(),
                  Icon(
                    Icons.open_in_new_rounded,
                    color: Colors.white.withValues(alpha: 0.6),
                    size: 16,
                  ),
                ],
              ),
              const Spacer(),
              Text(
                'TOTAL TRIPS',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w500,
                  color: Colors.white.withValues(alpha: 0.8),
                  letterSpacing: 1.0,
                ),
              ),
              const SizedBox(height: 6),
              Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text(
                    '42',
                    style: TextStyle(
                      fontSize: 48,
                      fontWeight: FontWeight.w700,
                      color: Colors.white,
                      height: 1.0,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Container(
                    margin: const EdgeInsets.only(bottom: 4),
                    padding: const EdgeInsets.symmetric(
                        horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.success.withValues(alpha: 0.2),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                          color: AppColors.success.withValues(alpha: 0.3)),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.trending_up_rounded,
                            color: AppColors.success, size: 13),
                        const SizedBox(width: 3),
                        Text(
                          '12%',
                          style: TextStyle(
                            fontSize: 12,
                            color: AppColors.success,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _UnassignedTripsCard extends StatelessWidget {
  const _UnassignedTripsCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: _whiteCardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const _Chip(label: 'Pending'),
              const Spacer(),
              Icon(Icons.more_horiz_rounded,
                  color: const Color(0xFF9CA3AF), size: 20),
            ],
          ),
          const Spacer(),
          const Text(
            'UNASSIGNED TRIPS',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w500,
              color: AppColors.brandGray,
              letterSpacing: 1.0,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              const Text(
                '8',
                style: TextStyle(
                  fontSize: 48,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.0,
                ),
              ),
              const SizedBox(width: 8),
              const Padding(
                padding: EdgeInsets.only(bottom: 6),
                child: Text(
                  'Require dispatch',
                  style: TextStyle(
                      fontSize: 13, color: AppColors.textSecondary),
                ),
              ),
            ],
          ),
          const Divider(height: 28, color: Color(0xFFF9FAFB), thickness: 1),
          Row(
            children: [
              const Text(
                'Assign drivers',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(width: 4),
              const Icon(Icons.arrow_forward_rounded,
                  size: 13, color: AppColors.primary),
            ],
          ),
        ],
      ),
    );
  }
}

class _ActiveDriversCard extends StatelessWidget {
  const _ActiveDriversCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: _whiteCardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _Chip(label: 'Status'),
          const Spacer(),
          const Text(
            'ACTIVE DRIVERS',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w500,
              color: AppColors.brandGray,
              letterSpacing: 1.0,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              const Text(
                '34',
                style: TextStyle(
                  fontSize: 48,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.0,
                ),
              ),
              const SizedBox(width: 8),
              const Padding(
                padding: EdgeInsets.only(bottom: 6),
                child: Text(
                  '/ 45 total',
                  style: TextStyle(
                      fontSize: 13, color: AppColors.textSecondary),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              for (int i = 0; i < 10; i++)
                Expanded(
                  child: Container(
                    margin: const EdgeInsets.symmetric(horizontal: 1),
                    height: 28,
                    decoration: BoxDecoration(
                      color: i < 7
                          ? AppColors.success
                          : const Color(0xFFF3F4F6),
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _PendingInspectionsCard extends StatelessWidget {
  const _PendingInspectionsCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: _whiteCardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const _Chip(label: 'Alerts'),
          const Spacer(),
          const Text(
            'PENDING INSPECTIONS',
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w500,
              color: AppColors.brandGray,
              letterSpacing: 1.0,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              const Text(
                '5',
                style: TextStyle(
                  fontSize: 48,
                  fontWeight: FontWeight.w700,
                  color: AppColors.warning,
                  height: 1.0,
                ),
              ),
              const SizedBox(width: 8),
              const Padding(
                padding: EdgeInsets.only(bottom: 6),
                child: Text(
                  'Pre-trip needed',
                  style: TextStyle(
                      fontSize: 13, color: AppColors.textSecondary),
                ),
              ),
            ],
          ),
          const Divider(height: 28, color: Color(0xFFF9FAFB), thickness: 1),
          Row(
            children: [
              const Text(
                'View vehicles',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                  color: AppColors.warning,
                ),
              ),
              const SizedBox(width: 4),
              const Icon(Icons.arrow_forward_rounded,
                  size: 13, color: AppColors.warning),
            ],
          ),
        ],
      ),
    );
  }
}

// ─── Dispatch Board ───────────────────────────────────────────────────────────

class _DispatchCard extends StatelessWidget {
  const _DispatchCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: _whiteCardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 20, 24, 16),
            child: Row(
              children: [
                const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      "Today's Dispatch Board",
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF111827),
                      ),
                    ),
                    SizedBox(height: 2),
                    Text(
                      'Live view of ongoing and upcoming trips',
                      style: TextStyle(
                          fontSize: 13,
                          color: AppColors.textSecondary),
                    ),
                  ],
                ),
                const Spacer(),
                Text(
                  'View All',
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: AppColors.primary,
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFFF9FAFB), thickness: 1),
          // Column headers
          Container(
            color: const Color(0xFFF9FAFB),
            padding:
                const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
            child: Row(
              children: const [
                Expanded(
                    flex: 2, child: _ColHeader('TRIP ID / TIME')),
                Expanded(
                    flex: 3, child: _ColHeader('CLIENT & ROUTE')),
                Expanded(flex: 2, child: _ColHeader('DRIVER')),
                Expanded(flex: 2, child: _ColHeader('STATUS')),
                SizedBox(width: 80, child: _ColHeader('ACTIONS', rightAlign: true)),
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFFF3F4F6), thickness: 1),
          // Row 1 — In Transit
          _TripRow(
            id: 'TRP-0842',
            time: '08:00 AM',
            client: 'Alamos Gold',
            route: 'Site A → Main Camp',
            driverInitials: 'JD',
            driverName: 'John Doe',
            status: 'In Transit',
            statusColor: AppColors.secondary,
            actionWidget: IconButton(
              icon: const Icon(Icons.chevron_right_rounded),
              color: const Color(0xFF9CA3AF),
              onPressed: () {},
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(),
            ),
          ),
          const Divider(height: 1, indent: 24, endIndent: 24, color: Color(0xFFF9FAFB), thickness: 1),
          // Row 2 — Pending/Unassigned
          _TripRow(
            id: 'TRP-0843',
            time: '10:30 AM',
            client: 'Northern Mining',
            route: 'Airport → Site B',
            isUnassigned: true,
            status: 'Pending',
            statusColor: AppColors.brandGray,
            actionWidget: SizedBox(
              height: 32,
              child: ElevatedButton(
                onPressed: () {},
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                  elevation: 0,
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(8)),
                  textStyle: const TextStyle(
                      fontSize: 12, fontWeight: FontWeight.w500),
                ),
                child: const Text('Dispatch'),
              ),
            ),
          ),
          const SizedBox(height: 4),
        ],
      ),
    );
  }
}

class _ColHeader extends StatelessWidget {
  final String text;
  final bool rightAlign;
  const _ColHeader(this.text, {this.rightAlign = false});

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: rightAlign ? TextAlign.right : TextAlign.left,
      style: const TextStyle(
        fontSize: 11,
        fontWeight: FontWeight.w500,
        color: AppColors.brandGray,
        letterSpacing: 0.5,
      ),
    );
  }
}

class _TripRow extends StatelessWidget {
  final String id;
  final String time;
  final String client;
  final String route;
  final String? driverInitials;
  final String? driverName;
  final bool isUnassigned;
  final String status;
  final Color statusColor;
  final Widget actionWidget;

  const _TripRow({
    required this.id,
    required this.time,
    required this.client,
    required this.route,
    this.driverInitials,
    this.driverName,
    this.isUnassigned = false,
    required this.status,
    required this.statusColor,
    required this.actionWidget,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
      child: Row(
        children: [
          // Trip ID + Time
          Expanded(
            flex: 2,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  id,
                  style: const TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF111827),
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  time,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondary),
                ),
              ],
            ),
          ),
          // Client + Route
          Expanded(
            flex: 3,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  client,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: Color(0xFF111827),
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  route,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondary),
                ),
              ],
            ),
          ),
          // Driver
          Expanded(
            flex: 2,
            child: isUnassigned
                ? Row(
                    children: const [
                      Icon(Icons.warning_amber_rounded,
                          color: AppColors.warning, size: 14),
                      SizedBox(width: 4),
                      Text(
                        'Unassigned',
                        style: TextStyle(
                          fontSize: 13,
                          color: AppColors.warning,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  )
                : Row(
                    children: [
                      Container(
                        width: 24,
                        height: 24,
                        decoration: const BoxDecoration(
                          color: AppColors.primary,
                          shape: BoxShape.circle,
                        ),
                        child: Center(
                          child: Text(
                            driverInitials!,
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 9,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        driverName!,
                        style: const TextStyle(
                            fontSize: 13, color: Color(0xFF374151)),
                      ),
                    ],
                  ),
          ),
          // Status badge
          Expanded(
            flex: 2,
            child: _StatusBadge(label: status, color: statusColor),
          ),
          // Action
          SizedBox(
            width: 80,
            child: Align(
              alignment: Alignment.centerRight,
              child: actionWidget,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final String label;
  final Color color;
  const _StatusBadge({required this.label, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.2)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w500,
              color: color,
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Quick Actions ────────────────────────────────────────────────────────────

class _QuickActionsCard extends StatelessWidget {
  const _QuickActionsCard();

  static const _actions = [
    (Icons.description_outlined, 'Create Manifest'),
    (Icons.send_rounded, 'Send Update'),
    (Icons.checklist_rounded, 'Log Inspection'),
    (Icons.receipt_long_rounded, 'Generate Invoice'),
  ];

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: _whiteCardDecoration(),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Quick Actions',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            ),
          ),
          const SizedBox(height: 16),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisSpacing: 10,
            mainAxisSpacing: 10,
            childAspectRatio: 1.1,
            children: _actions
                .map((a) =>
                    _QuickActionButton(icon: a.$1, label: a.$2))
                .toList(),
          ),
        ],
      ),
    );
  }
}

class _QuickActionButton extends StatefulWidget {
  final IconData icon;
  final String label;
  const _QuickActionButton({required this.icon, required this.label});

  @override
  State<_QuickActionButton> createState() => _QuickActionButtonState();
}

class _QuickActionButtonState extends State<_QuickActionButton> {
  bool _hovered = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      onEnter: (_) => setState(() => _hovered = true),
      onExit: (_) => setState(() => _hovered = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        decoration: BoxDecoration(
          color: _hovered
              ? AppColors.primary.withValues(alpha: 0.05)
              : const Color(0xFFF9FAFB),
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: _hovered
                ? AppColors.primary.withValues(alpha: 0.3)
                : const Color(0xFFF3F4F6),
          ),
        ),
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: () {},
            borderRadius: BorderRadius.circular(12),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(
                    widget.icon,
                    size: 24,
                    color: _hovered
                        ? AppColors.primary
                        : const Color(0xFF9CA3AF),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    widget.label,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w500,
                      color: _hovered
                          ? AppColors.primary
                          : const Color(0xFF374151),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

// ─── Shared chip widget ───────────────────────────────────────────────────────

class _Chip extends StatelessWidget {
  final String label;
  final bool isLight;
  const _Chip({required this.label, this.isLight = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: isLight
            ? Colors.white.withValues(alpha: 0.15)
            : const Color(0xFFF3F4F6),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(
          color: isLight
              ? Colors.white.withValues(alpha: 0.25)
              : const Color(0xFFE5E7EB),
        ),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w500,
          color: isLight ? Colors.white : const Color(0xFF4B5563),
        ),
      ),
    );
  }
}
