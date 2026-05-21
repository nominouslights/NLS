import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/routing/route_names.dart';
import '../../../../core/theme/app_colors.dart';
import '../providers/register_provider.dart';

class RegisterPage extends ConsumerStatefulWidget {
  const RegisterPage({super.key});

  @override
  ConsumerState<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends ConsumerState<RegisterPage> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _obscurePassword = true;
  bool _obscureConfirm = true;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final success = await ref
        .read(registerProvider.notifier)
        .register(_emailController.text.trim(), _passwordController.text);
    if (!mounted) return;
    if (success) {
      context.go(RouteNames.pendingApproval);
    } else {
      final error = ref.read(registerProvider).error;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error?.toString().replaceAll('Exception: ', '') ??
              'Registration failed.'),
          backgroundColor: Colors.red.shade700,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(registerProvider).isLoading;
    final isWide = MediaQuery.of(context).size.width >= 900;

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FA),
      body: isWide ? _wideLayout(isLoading) : _narrowLayout(isLoading),
    );
  }

  Widget _wideLayout(bool isLoading) => Row(
        children: [
          Expanded(flex: 5, child: _brandingPanel()),
          Expanded(
            flex: 4,
            child: Center(child: _card(isLoading)),
          ),
        ],
      );

  Widget _narrowLayout(bool isLoading) => Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: _card(isLoading),
        ),
      );

  Widget _brandingPanel() => Container(
        color: const Color(0xFF005493),
        padding: const EdgeInsets.all(48),
        child: const Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.directions_bus_rounded, color: Colors.white, size: 48),
            SizedBox(height: 16),
            Text(
              'Northern Link\nShuttle Management',
              style: TextStyle(
                color: Colors.white,
                fontSize: 28,
                fontWeight: FontWeight.bold,
                height: 1.3,
              ),
            ),
            SizedBox(height: 12),
            Text(
              'Register as a driver to join the fleet.',
              style: TextStyle(color: Colors.white70, fontSize: 16),
            ),
          ],
        ),
      );

  Widget _card(bool isLoading) => ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: Card(
          elevation: 4,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Header
                const Padding(
                  padding: EdgeInsets.all(24),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Driver Registration',
                        style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
                      ),
                      SizedBox(height: 4),
                      Text(
                        'Create your account. An admin will review your request.',
                        style: TextStyle(fontSize: 13, color: Color(0xFF6B7280)),
                      ),
                    ],
                  ),
                ),
                const Divider(height: 1),
                // Fields
                Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                        decoration: const InputDecoration(
                          labelText: 'Email',
                          hintText: 'name@example.com',
                          prefixIcon:
                              Icon(Icons.email_outlined, color: AppColors.brandGray),
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) return 'Email is required';
                          if (!v.contains('@')) return 'Enter a valid email';
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _passwordController,
                        obscureText: _obscurePassword,
                        decoration: InputDecoration(
                          labelText: 'Password',
                          hintText: '••••••••',
                          prefixIcon: const Icon(Icons.lock_outline,
                              color: AppColors.brandGray),
                          suffixIcon: IconButton(
                            icon: Icon(
                              _obscurePassword
                                  ? Icons.visibility_off
                                  : Icons.visibility,
                              color: AppColors.brandGray,
                            ),
                            onPressed: () =>
                                setState(() => _obscurePassword = !_obscurePassword),
                          ),
                        ),
                        validator: (v) {
                          if (v == null || v.isEmpty) return 'Password is required';
                          if (v.length < 8) return 'Minimum 8 characters';
                          return null;
                        },
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _confirmController,
                        obscureText: _obscureConfirm,
                        decoration: InputDecoration(
                          labelText: 'Confirm Password',
                          hintText: '••••••••',
                          prefixIcon: const Icon(Icons.lock_outline,
                              color: AppColors.brandGray),
                          suffixIcon: IconButton(
                            icon: Icon(
                              _obscureConfirm
                                  ? Icons.visibility_off
                                  : Icons.visibility,
                              color: AppColors.brandGray,
                            ),
                            onPressed: () =>
                                setState(() => _obscureConfirm = !_obscureConfirm),
                          ),
                        ),
                        validator: (v) {
                          if (v != _passwordController.text) {
                            return 'Passwords do not match';
                          }
                          return null;
                        },
                      ),
                      const SizedBox(height: 24),
                      SizedBox(
                        height: 52,
                        child: ElevatedButton(
                          onPressed: isLoading ? null : _submit,
                          child: isLoading
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                      color: Colors.white, strokeWidth: 2),
                                )
                              : const Text('Register as Driver'),
                        ),
                      ),
                    ],
                  ),
                ),
                // Footer
                Container(
                  decoration: const BoxDecoration(
                    color: Color(0xFFF9FAFB),
                    border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
                  ),
                  padding: const EdgeInsets.all(16),
                  child: Center(
                    child: TextButton(
                      onPressed: () => context.go(RouteNames.login),
                      child: const Text(
                        'Already have an account? Sign in',
                        style: TextStyle(fontSize: 13, color: Color(0xFF005493)),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
}
