import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/routing/route_names.dart';

class PendingApprovalPage extends StatelessWidget {
  const PendingApprovalPage({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: const Color(0xFFF5F7FA),
        body: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Container(
                    padding: const EdgeInsets.all(24),
                    decoration: BoxDecoration(
                      color: const Color(0xFFEFF6FF),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(
                      Icons.hourglass_top_rounded,
                      size: 56,
                      color: Color(0xFF005493),
                    ),
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    'Registration Submitted',
                    style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Your account is pending admin approval. You will be able to log in once an administrator has reviewed your request.',
                    style: TextStyle(fontSize: 15, color: Color(0xFF6B7280), height: 1.5),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 32),
                  OutlinedButton.icon(
                    onPressed: () => context.go(RouteNames.login),
                    icon: const Icon(Icons.arrow_back),
                    label: const Text('Back to Sign In'),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 24, vertical: 14),
                      side: const BorderSide(color: Color(0xFF005493)),
                      foregroundColor: const Color(0xFF005493),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
}
