import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/app_mode/app_mode_provider.dart';
import '../../../../core/routing/route_names.dart';
import '../../../../core/theme/app_colors.dart';

class ModeSelectionPage extends ConsumerWidget {
  const ModeSelectionPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FA),
      body: SafeArea(
        child: Column(
          children: [
            _buildHeader(context),
            Expanded(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 24),
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 860),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const SizedBox(height: 16),
                        Text(
                          'Select Service Mode',
                          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                                fontWeight: FontWeight.w700,
                                color: const Color(0xFF111827),
                              ),
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'Choose which type of service you are managing today.',
                          style: TextStyle(
                            fontSize: 15,
                            color: AppColors.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 40),
                        LayoutBuilder(
                          builder: (context, constraints) {
                            final wide = constraints.maxWidth >= 560;
                            if (wide) {
                              return Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Expanded(
                                    child: _ModeCard(
                                      icon: Icons.work_outline_rounded,
                                      title: 'Charter / Employee Shuttle',
                                      description:
                                          'Client-booked trips, PO numbers, full vehicle charters.',
                                      buttonLabel: 'Enter Charter Mode',
                                      onTap: () => _select(context, ref, AppMode.charter),
                                    ),
                                  ),
                                  const SizedBox(width: 24),
                                  Expanded(
                                    child: _ModeCard(
                                      icon: Icons.people_outline_rounded,
                                      title: 'Community Per-Seat Runs',
                                      description:
                                          'Sell individual seats, manage walk-in bookings, per-seat pricing.',
                                      buttonLabel: 'Enter Community Mode',
                                      accent: true,
                                      onTap: () => _select(context, ref, AppMode.community),
                                    ),
                                  ),
                                ],
                              );
                            }
                            return Column(
                              children: [
                                _ModeCard(
                                  icon: Icons.work_outline_rounded,
                                  title: 'Charter / Employee Shuttle',
                                  description:
                                      'Client-booked trips, PO numbers, full vehicle charters.',
                                  buttonLabel: 'Enter Charter Mode',
                                  onTap: () => _select(context, ref, AppMode.charter),
                                ),
                                const SizedBox(height: 20),
                                _ModeCard(
                                  icon: Icons.people_outline_rounded,
                                  title: 'Community Per-Seat Runs',
                                  description:
                                      'Sell individual seats, manage walk-in bookings, per-seat pricing.',
                                  buttonLabel: 'Enter Community Mode',
                                  accent: true,
                                  onTap: () => _select(context, ref, AppMode.community),
                                ),
                              ],
                            );
                          },
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _select(BuildContext context, WidgetRef ref, AppMode mode) async {
    await ref.read(appModeProvider.notifier).setMode(mode);
    if (!context.mounted) return;
    if (mode == AppMode.charter) {
      context.go(RouteNames.home);
    } else {
      context.go(RouteNames.community);
    }
  }

  Widget _buildHeader(BuildContext context) {
    return Container(
      height: 64,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.primary,
              borderRadius: BorderRadius.circular(10),
              boxShadow: [
                BoxShadow(
                  color: AppColors.primary.withValues(alpha: 0.3),
                  blurRadius: 20,
                ),
              ],
            ),
            child: const Icon(
              Icons.local_shipping_rounded,
              color: Colors.white,
              size: 20,
            ),
          ),
          const SizedBox(width: 12),
          const Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Northern Link',
                style: TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.2,
                ),
              ),
              Text(
                'SHUTTLE MANAGEMENT',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w500,
                  color: AppColors.brandGray,
                  letterSpacing: 0.8,
                  height: 1.2,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ModeCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String description;
  final String buttonLabel;
  final bool accent;
  final VoidCallback onTap;

  const _ModeCard({
    required this.icon,
    required this.title,
    required this.description,
    required this.buttonLabel,
    this.accent = false,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final color = accent ? const Color(0xFF0F766E) : AppColors.primary;
    final bgColor = accent ? const Color(0xFFF0FDFA) : const Color(0xFFEFF6FF);

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: const Color(0xFFE5E7EB)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.06),
            blurRadius: 24,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      padding: const EdgeInsets.all(32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: bgColor,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Icon(icon, color: color, size: 28),
          ),
          const SizedBox(height: 20),
          Text(
            title,
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            ),
          ),
          const SizedBox(height: 8),
          Text(
            description,
            style: const TextStyle(
              fontSize: 14,
              color: Color(0xFF6B7280),
              height: 1.5,
            ),
          ),
          const SizedBox(height: 28),
          SizedBox(
            width: double.infinity,
            height: 48,
            child: ElevatedButton(
              onPressed: onTap,
              style: ElevatedButton.styleFrom(
                backgroundColor: color,
                foregroundColor: Colors.white,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(12),
                ),
                elevation: 0,
              ),
              child: Text(
                buttonLabel,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
