import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';

class BrandingPanel extends StatelessWidget {
  final double fontSize;
  const BrandingPanel({super.key, required this.fontSize});

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // Logo block
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: 0.08),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 56,
                height: 56,
                decoration: BoxDecoration(
                  color: const Color(0xFF005493),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(
                  Icons.local_shipping,
                  color: Colors.white,
                  size: 28,
                ),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Northern Link',
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    'Shuttle Management',
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.brandGray,
                      letterSpacing: 1.5,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 32),
        RichText(
          text: TextSpan(
            style: TextStyle(
              fontSize: fontSize,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
              height: 1.2,
            ),
            children: const [
              TextSpan(text: 'Streamline your '),
              TextSpan(
                text: 'logistics',
                style: TextStyle(color: Color(0xFF005493)),
              ),
              TextSpan(text: ' operations'),
            ],
          ),
        ),
        const SizedBox(height: 8),
        Text(
          'Secure access to the unified platform for dispatchers, drivers, and clients.',
          style: TextStyle(
            fontSize: 16,
            color: AppColors.textSecondary,
          ),
        ),
        const SizedBox(height: 24),
        Row(
          children: [
            _TrustBadge(
              icon: Icons.security,
              iconColor: const Color(0xFF28A745),
              label: 'Secure Connection',
            ),
            const SizedBox(width: 12),
            _TrustBadge(
              icon: Icons.dns,
              iconColor: const Color(0xFF005493),
              label: 'System Online',
            ),
          ],
        ),
      ],
    );
  }
}

class _TrustBadge extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final String label;

  const _TrustBadge({
    required this.icon,
    required this.iconColor,
    required this.label,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(50),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, color: iconColor, size: 16),
          const SizedBox(width: 6),
          Text(label, style: const TextStyle(fontSize: 13)),
        ],
      ),
    );
  }
}
