import 'package:flutter/material.dart';
import '../../../../core/theme/app_colors.dart';

class LoginCard extends StatelessWidget {
  final GlobalKey<FormState> formKey;
  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool obscurePassword;
  final int selectedRole;
  final bool isLoading;
  final VoidCallback onToggleObscure;
  final ValueChanged<int> onRoleSelected;
  final VoidCallback onSubmit;
  final VoidCallback onRegister;

  static const _roles = [
    (label: 'Admin', icon: Icons.business_center),
    (label: 'Dispatch', icon: Icons.headset_mic),
    (label: 'Driver', icon: Icons.drive_eta),
    (label: 'Client', icon: Icons.business),
  ];

  const LoginCard({
    super.key,
    required this.formKey,
    required this.emailController,
    required this.passwordController,
    required this.obscurePassword,
    required this.selectedRole,
    required this.isLoading,
    required this.onToggleObscure,
    required this.onRoleSelected,
    required this.onSubmit,
    required this.onRegister,
  });

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(24),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          border: Border.all(color: const Color(0xFFE5E7EB)),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.08),
              blurRadius: 40,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Card header
              Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Sign In',
                      style: TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Enter your credentials to access your dashboard.',
                      style: TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              const Divider(height: 1),
              // Card body
              Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // Email field
                    TextFormField(
                      controller: emailController,
                      keyboardType: TextInputType.emailAddress,
                      decoration: const InputDecoration(
                        hintText: 'name@company.com',
                        prefixIcon: Icon(
                          Icons.email_outlined,
                          color: AppColors.brandGray,
                        ),
                      ),
                      validator: (v) =>
                          (v == null || v.isEmpty) ? 'Email is required' : null,
                    ),
                    const SizedBox(height: 16),
                    // Password label row
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        const Text(
                          'Password',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                        TextButton(
                          onPressed: () {},
                          style: TextButton.styleFrom(
                            padding: EdgeInsets.zero,
                            minimumSize: Size.zero,
                            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                          ),
                          child: const Text(
                            'Forgot password?',
                            style: TextStyle(
                              fontSize: 13,
                              color: Color(0xFF005493),
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    // Password field
                    TextFormField(
                      controller: passwordController,
                      obscureText: obscurePassword,
                      decoration: InputDecoration(
                        hintText: '••••••••',
                        prefixIcon: const Icon(
                          Icons.lock_outline,
                          color: AppColors.brandGray,
                        ),
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscurePassword
                                ? Icons.visibility_off
                                : Icons.visibility,
                            color: AppColors.brandGray,
                          ),
                          onPressed: onToggleObscure,
                        ),
                      ),
                      validator: (v) =>
                          (v == null || v.isEmpty) ? 'Password is required' : null,
                    ),
                    const SizedBox(height: 16),
                    // Role selector label
                    Text(
                      'CONNECTING AS',
                      style: TextStyle(
                        fontSize: 11,
                        color: AppColors.textSecondary,
                        letterSpacing: 1.5,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                    const SizedBox(height: 8),
                    // Role tiles
                    Row(
                      children: List.generate(_roles.length, (i) {
                        final role = _roles[i];
                        final isSelected = selectedRole == i;
                        return Expanded(
                          child: Padding(
                            padding: EdgeInsets.only(
                              left: i == 0 ? 0 : 4,
                              right: i == _roles.length - 1 ? 0 : 4,
                            ),
                            child: InkWell(
                              onTap: () => onRoleSelected(i),
                              borderRadius: BorderRadius.circular(8),
                              child: AnimatedContainer(
                                duration: const Duration(milliseconds: 150),
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: isSelected
                                      ? const Color(0xFFEFF6FF)
                                      : const Color(0xFFF9FAFB),
                                  borderRadius: BorderRadius.circular(8),
                                  border: Border.all(
                                    color: isSelected
                                        ? const Color(0xFF005493)
                                        : const Color(0xFFF3F4F6),
                                    width: isSelected ? 2 : 1,
                                  ),
                                ),
                                child: Column(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Icon(
                                      role.icon,
                                      size: 18,
                                      color: isSelected
                                          ? const Color(0xFF005493)
                                          : AppColors.brandGray,
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      role.label,
                                      style: TextStyle(
                                        fontSize: 10,
                                        fontWeight: isSelected
                                            ? FontWeight.w600
                                            : FontWeight.w500,
                                        color: isSelected
                                            ? const Color(0xFF005493)
                                            : AppColors.textSecondary,
                                      ),
                                      textAlign: TextAlign.center,
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        );
                      }),
                    ),
                    const SizedBox(height: 14),
                    // Sign-in button
                    SizedBox(
                      width: double.infinity,
                      height: 52,
                      child: ElevatedButton(
                        onPressed: isLoading ? null : onSubmit,
                        child: isLoading
                            ? const SizedBox(
                                width: 20,
                                height: 20,
                                child: CircularProgressIndicator(
                                  color: Colors.white,
                                  strokeWidth: 2,
                                ),
                              )
                            : const Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Text('Sign In to Dashboard'),
                                  SizedBox(width: 8),
                                  Icon(Icons.arrow_forward, size: 16),
                                ],
                              ),
                      ),
                    ),
                    const SizedBox(height: 5),
                    Center(
                      child: TextButton(
                        onPressed: onRegister,
                        style: TextButton.styleFrom(
                          foregroundColor: const Color(0xFF005493),
                        ),
                        child: const Text(
                          'New driver? Register here',
                          style: TextStyle(fontSize: 12),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              // Card footer
              Container(
                decoration: const BoxDecoration(
                  color: Color(0xFFF9FAFB),
                  border: Border(
                    top: BorderSide(color: Color(0xFFE5E7EB)),
                  ),
                ),
                padding: const EdgeInsets.all(10),
                child: Wrap(
                  alignment: WrapAlignment.center,
                  children: [
                    Text(
                      'By signing in, you agree to the ',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    GestureDetector(
                      onTap: () {},
                      child: const Text(
                        'Terms',
                        style: TextStyle(
                          fontSize: 12,
                          color: Color(0xFF005493),
                        ),
                      ),
                    ),
                    Text(
                      ' and ',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    GestureDetector(
                      onTap: () {},
                      child: const Text(
                        'Privacy Policy',
                        style: TextStyle(
                          fontSize: 12,
                          color: Color(0xFF005493),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
