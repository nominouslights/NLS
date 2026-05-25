import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/driver.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../providers/driver_form_provider.dart';
import '../providers/drivers_provider.dart';

class DriverFormSheet extends ConsumerStatefulWidget {
  final Driver? driver;

  const DriverFormSheet({super.key, this.driver});

  @override
  ConsumerState<DriverFormSheet> createState() => _DriverFormSheetState();
}

class _DriverFormSheetState extends ConsumerState<DriverFormSheet>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final _formKey = GlobalKey<FormState>();

  // Personal Info
  final _firstNameCtrl = TextEditingController();
  final _lastNameCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();

  // Employment
  final _employeeIdCtrl = TextEditingController();
  DateTime _hireDate = DateTime.now();
  bool _isActive = true;

  bool get _isEditing => widget.driver != null;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _tabController.addListener(() {
      if (mounted) setState(() {});
    });
    if (_isEditing) _populate(widget.driver!);
  }

  void _populate(Driver d) {
    _firstNameCtrl.text = d.firstName;
    _lastNameCtrl.text = d.lastName;
    _phoneCtrl.text = d.phone;
    _emailCtrl.text = d.email;
    _employeeIdCtrl.text = d.employeeId;
    _hireDate = d.hireDate;
    _isActive = d.isActive;
  }

  @override
  void dispose() {
    _tabController.dispose();
    for (final c in [
      _firstNameCtrl,
      _lastNameCtrl,
      _phoneCtrl,
      _emailCtrl,
      _employeeIdCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  int _firstTabWithErrors() {
    final emailOk =
        RegExp(r'^[^@]+@[^@]+\.[^@]+').hasMatch(_emailCtrl.text.trim());
    if (_firstNameCtrl.text.trim().isEmpty ||
        _lastNameCtrl.text.trim().isEmpty ||
        _phoneCtrl.text.trim().isEmpty ||
        !emailOk) { return 0; }
    if (_employeeIdCtrl.text.trim().isEmpty) { return 1; }
    return _tabController.index;
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      _tabController.animateTo(_firstTabWithErrors());
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
              content: Text('Please correct the highlighted fields.')),
        );
      }
      return;
    }

    try {
      if (_isEditing) {
        await ref.read(driverFormProvider.notifier).updateDriver(
              widget.driver!.id,
              UpdateDriverParams(
                employeeId: _employeeIdCtrl.text.trim(),
                firstName: _firstNameCtrl.text.trim(),
                lastName: _lastNameCtrl.text.trim(),
                phone: _phoneCtrl.text.trim(),
                email: _emailCtrl.text.trim(),
                hireDate: _hireDate,
                isActive: _isActive,
              ),
            );
      } else {
        await ref.read(driverFormProvider.notifier).createDriver(
              CreateDriverParams(
                employeeId: _employeeIdCtrl.text.trim(),
                firstName: _firstNameCtrl.text.trim(),
                lastName: _lastNameCtrl.text.trim(),
                phone: _phoneCtrl.text.trim(),
                email: _emailCtrl.text.trim(),
                hireDate: _hireDate,
              ),
            );
      }
      if (mounted) {
        ref.invalidate(driversProvider);
        Navigator.of(context).pop(true);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text(e.toString()),
              backgroundColor: AppColors.danger),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(driverFormProvider).isLoading;
    final keyboardHeight = MediaQuery.of(context).viewInsets.bottom;
    final safeBottom = MediaQuery.of(context).padding.bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
        height: MediaQuery.of(context).size.height * 0.85,
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: Column(
          children: [
            // Handle
            const SizedBox(height: 12),
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: const Color(0xFFD1D5DB),
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            // Header
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 16, 12, 0),
              child: Row(
                children: [
                  Text(
                    _isEditing ? 'Edit Driver' : 'New Driver',
                    style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF111827)),
                  ),
                  const Spacer(),
                  IconButton(
                    icon: const Icon(Icons.close_rounded),
                    onPressed: () => Navigator.of(context).pop(),
                  ),
                ],
              ),
            ),
            // Tabs
            TabBar(
              controller: _tabController,
              labelColor: AppColors.primary,
              unselectedLabelColor: AppColors.brandGray,
              indicatorColor: AppColors.primary,
              tabs: const [
                Tab(text: 'Personal Info'),
                Tab(text: 'Employment'),
              ],
            ),
            const Divider(height: 1),
            // Body
            Expanded(
              child: Form(
                key: _formKey,
                child: IndexedStack(
                  index: _tabController.index,
                  children: [
                    _PersonalInfoTab(
                      firstNameCtrl: _firstNameCtrl,
                      lastNameCtrl: _lastNameCtrl,
                      phoneCtrl: _phoneCtrl,
                      emailCtrl: _emailCtrl,
                    ),
                    _EmploymentTab(
                      employeeIdCtrl: _employeeIdCtrl,
                      hireDate: _hireDate,
                      isActive: _isActive,
                      isEditing: _isEditing,
                      onHireDateChanged: (d) =>
                          setState(() => _hireDate = d),
                      onActiveChanged: (v) => setState(() => _isActive = v),
                    ),
                  ],
                ),
              ),
            ),
            // Footer
            Container(
              padding: EdgeInsets.fromLTRB(
                  16, 12, 16, (keyboardHeight > 0 ? 12 : safeBottom + 12)),
              decoration: const BoxDecoration(
                border: Border(top: BorderSide(color: Color(0xFFE5E7EB))),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Cancel'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    flex: 2,
                    child: FilledButton(
                      onPressed: isLoading ? null : _submit,
                      style: FilledButton.styleFrom(
                          backgroundColor: AppColors.primary),
                      child: isLoading
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                  strokeWidth: 2, color: Colors.white))
                          : Text(_isEditing
                              ? 'Save Changes'
                              : 'Create Driver'),
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
}

// ── Tab Sections ──────────────────────────────────────────────────────────────

class _PersonalInfoTab extends StatelessWidget {
  final TextEditingController firstNameCtrl;
  final TextEditingController lastNameCtrl;
  final TextEditingController phoneCtrl;
  final TextEditingController emailCtrl;

  const _PersonalInfoTab({
    required this.firstNameCtrl,
    required this.lastNameCtrl,
    required this.phoneCtrl,
    required this.emailCtrl,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Row(
          children: [
            Expanded(
              child: _Field(
                  controller: firstNameCtrl,
                  label: 'First Name',
                  required: true),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _Field(
                  controller: lastNameCtrl,
                  label: 'Last Name',
                  required: true),
            ),
          ],
        ),
        const SizedBox(height: 16),
        _Field(
          controller: phoneCtrl,
          label: 'Phone Number',
          required: true,
          keyboardType: TextInputType.phone,
        ),
        const SizedBox(height: 16),
        _Field(
          controller: emailCtrl,
          label: 'Email Address',
          required: true,
          keyboardType: TextInputType.emailAddress,
          validator: (v) {
            if (v == null || v.isEmpty) return 'Email is required';
            if (!RegExp(r'^[^@]+@[^@]+\.[^@]+').hasMatch(v)) {
              return 'Enter a valid email';
            }
            return null;
          },
        ),
      ],
    );
  }
}

class _EmploymentTab extends StatelessWidget {
  final TextEditingController employeeIdCtrl;
  final DateTime hireDate;
  final bool isActive;
  final bool isEditing;
  final ValueChanged<DateTime> onHireDateChanged;
  final ValueChanged<bool> onActiveChanged;

  const _EmploymentTab({
    required this.employeeIdCtrl,
    required this.hireDate,
    required this.isActive,
    required this.isEditing,
    required this.onHireDateChanged,
    required this.onActiveChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _Field(
          controller: employeeIdCtrl,
          label: 'Employee ID',
          required: true,
          hintText: 'e.g. EMP-001',
        ),
        const SizedBox(height: 16),
        // Hire date picker
        InkWell(
          onTap: () async {
            final picked = await showDatePicker(
              context: context,
              initialDate: hireDate,
              firstDate: DateTime(2000),
              lastDate: DateTime.now(),
            );
            if (picked != null) onHireDateChanged(picked);
          },
          borderRadius: BorderRadius.circular(12),
          child: InputDecorator(
            decoration: InputDecoration(
              labelText: 'Hire Date',
              suffixIcon: const Icon(Icons.calendar_today_outlined, size: 18),
              border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12)),
            ),
            child: Text(
              '${hireDate.year}-${hireDate.month.toString().padLeft(2, '0')}-${hireDate.day.toString().padLeft(2, '0')}',
              style:
                  const TextStyle(fontSize: 14, color: Color(0xFF111827)),
            ),
          ),
        ),
        if (isEditing) ...[
          const SizedBox(height: 20),
          Row(
            children: [
              const Text('Active',
                  style: TextStyle(
                      fontSize: 14, fontWeight: FontWeight.w500)),
              const Spacer(),
              Switch(
                  value: isActive,
                  onChanged: onActiveChanged,
                  activeColor: AppColors.primary),
            ],
          ),
        ],
      ],
    );
  }
}

// ── Shared form widgets ───────────────────────────────────────────────────────

class _Field extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final bool required;
  final TextInputType? keyboardType;
  final String? Function(String?)? validator;
  final String? hintText;

  const _Field({
    required this.controller,
    required this.label,
    this.required = false,
    this.keyboardType,
    this.validator,
    this.hintText,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      decoration: InputDecoration(
        labelText: required ? '$label *' : label,
        hintText: hintText,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      validator: validator ??
          (required
              ? (v) =>
                  (v == null || v.trim().isEmpty) ? '$label is required' : null
              : null),
    );
  }
}
