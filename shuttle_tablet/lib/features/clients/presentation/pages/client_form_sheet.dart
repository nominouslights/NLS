import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../providers/client_form_provider.dart';
import '../providers/clients_provider.dart';

class ClientFormSheet extends ConsumerStatefulWidget {
  final Client? client;

  const ClientFormSheet({super.key, this.client});

  @override
  ConsumerState<ClientFormSheet> createState() => _ClientFormSheetState();
}

class _ClientFormSheetState extends ConsumerState<ClientFormSheet>
    with SingleTickerProviderStateMixin {
  late final TabController _tabController;
  final _formKey = GlobalKey<FormState>();

  // General Info
  final _businessNameCtrl = TextEditingController();
  ServiceType _serviceType = ServiceType.corporate;
  final _contactNameCtrl = TextEditingController();
  final _contactTitleCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();

  // Address
  final _streetCtrl = TextEditingController();
  final _cityCtrl = TextEditingController();
  final _provinceCtrl = TextEditingController();
  final _postalCtrl = TextEditingController();

  // Billing
  final _gstCtrl = TextEditingController();
  String _paymentMethod = 'EFT';
  int _netTerms = 30;

  // Compliance
  final _complianceCtrl = TextEditingController();
  bool _isMinesite = false;

  bool _isActive = true;

  bool get _isEditing => widget.client != null;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    if (_isEditing) _populate(widget.client!);
  }

  void _populate(Client c) {
    _businessNameCtrl.text = c.businessName;
    _serviceType = c.serviceType;
    _contactNameCtrl.text = c.primaryContactName;
    _contactTitleCtrl.text = c.primaryContactTitle;
    _phoneCtrl.text = c.phone;
    _emailCtrl.text = c.email;
    _streetCtrl.text = c.streetAddress;
    _cityCtrl.text = c.city;
    _provinceCtrl.text = c.province;
    _postalCtrl.text = c.postalCode;
    _gstCtrl.text = c.gstHstNumber ?? '';
    _paymentMethod = c.preferredPaymentMethod;
    _netTerms = c.netPaymentTerms;
    _complianceCtrl.text = c.complianceNotes ?? '';
    _isMinesite = c.isMinesite;
    _isActive = c.isActive;
  }

  @override
  void dispose() {
    _tabController.dispose();
    for (final c in [
      _businessNameCtrl, _contactNameCtrl, _contactTitleCtrl,
      _phoneCtrl, _emailCtrl, _streetCtrl, _cityCtrl, _provinceCtrl,
      _postalCtrl, _gstCtrl, _complianceCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    final params = UpdateClientParams(
      businessName: _businessNameCtrl.text.trim(),
      serviceType: _serviceType,
      primaryContactName: _contactNameCtrl.text.trim(),
      primaryContactTitle: _contactTitleCtrl.text.trim(),
      phone: _phoneCtrl.text.trim(),
      email: _emailCtrl.text.trim(),
      streetAddress: _streetCtrl.text.trim(),
      city: _cityCtrl.text.trim(),
      province: _provinceCtrl.text.trim().toUpperCase(),
      postalCode: _postalCtrl.text.trim().toUpperCase(),
      gstHstNumber: _gstCtrl.text.trim().isEmpty ? null : _gstCtrl.text.trim(),
      preferredPaymentMethod: _paymentMethod,
      netPaymentTerms: _netTerms,
      complianceNotes: _complianceCtrl.text.trim().isEmpty ? null : _complianceCtrl.text.trim(),
      isMinesite: _isMinesite,
      isActive: _isActive,
    );

    try {
      if (_isEditing) {
        await ref.read(clientFormProvider.notifier).updateClient(widget.client!.id, params);
      } else {
        await ref.read(clientFormProvider.notifier).createClient(params);
      }
      if (mounted) {
        ref.invalidate(clientsProvider);
        Navigator.of(context).pop(true);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString()), backgroundColor: AppColors.danger),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLoading = ref.watch(clientFormProvider).isLoading;
    final keyboardHeight = MediaQuery.of(context).viewInsets.bottom;
    final safeBottom = MediaQuery.of(context).padding.bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: keyboardHeight),
      child: Container(
      height: MediaQuery.of(context).size.height * 0.92,
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
                  _isEditing ? 'Edit Client' : 'New Client',
                  style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: Color(0xFF111827)),
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
            isScrollable: true,
            tabs: const [
              Tab(text: 'General Info'),
              Tab(text: 'Billing'),
              Tab(text: 'Address'),
              Tab(text: 'Compliance'),
            ],
          ),
          const Divider(height: 1),
          // Body
          Expanded(
            child: Form(
              key: _formKey,
              child: TabBarView(
                controller: _tabController,
                children: [
                  _GeneralInfoTab(
                    businessNameCtrl: _businessNameCtrl,
                    contactNameCtrl: _contactNameCtrl,
                    contactTitleCtrl: _contactTitleCtrl,
                    phoneCtrl: _phoneCtrl,
                    emailCtrl: _emailCtrl,
                    serviceType: _serviceType,
                    isActive: _isActive,
                    isEditing: _isEditing,
                    onServiceTypeChanged: (v) => setState(() => _serviceType = v),
                    onActiveChanged: (v) => setState(() => _isActive = v),
                  ),
                  _BillingTab(
                    gstCtrl: _gstCtrl,
                    paymentMethod: _paymentMethod,
                    netTerms: _netTerms,
                    onPaymentMethodChanged: (v) => setState(() => _paymentMethod = v),
                    onNetTermsChanged: (v) => setState(() => _netTerms = v),
                  ),
                  _AddressTab(
                    streetCtrl: _streetCtrl,
                    cityCtrl: _cityCtrl,
                    provinceCtrl: _provinceCtrl,
                    postalCtrl: _postalCtrl,
                  ),
                  _ComplianceTab(
                    complianceCtrl: _complianceCtrl,
                    isMinesite: _isMinesite,
                    onMineSiteChanged: (v) => setState(() => _isMinesite = v),
                  ),
                ],
              ),
            ),
          ),
          // Footer
          Container(
            padding: EdgeInsets.fromLTRB(16, 12, 16, (keyboardHeight > 0 ? 12 : safeBottom + 12)),
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
                    style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
                    child: isLoading
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : Text(_isEditing ? 'Save Changes' : 'Create Client'),
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

// ─── Tab Sections ─────────────────────────────────────────────────────────────

class _GeneralInfoTab extends StatelessWidget {
  final TextEditingController businessNameCtrl;
  final TextEditingController contactNameCtrl;
  final TextEditingController contactTitleCtrl;
  final TextEditingController phoneCtrl;
  final TextEditingController emailCtrl;
  final ServiceType serviceType;
  final bool isActive;
  final bool isEditing;
  final ValueChanged<ServiceType> onServiceTypeChanged;
  final ValueChanged<bool> onActiveChanged;

  const _GeneralInfoTab({
    required this.businessNameCtrl,
    required this.contactNameCtrl,
    required this.contactTitleCtrl,
    required this.phoneCtrl,
    required this.emailCtrl,
    required this.serviceType,
    required this.isActive,
    required this.isEditing,
    required this.onServiceTypeChanged,
    required this.onActiveChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _Field(controller: businessNameCtrl, label: 'Company Name', required: true),
        const SizedBox(height: 16),
        _DropdownField<ServiceType>(
          label: 'Service Type',
          value: serviceType,
          items: const [
            DropdownMenuItem(value: ServiceType.corporate, child: Text('Corporate')),
            DropdownMenuItem(value: ServiceType.community, child: Text('Community')),
          ],
          onChanged: onServiceTypeChanged,
        ),
        const SizedBox(height: 16),
        _Field(controller: contactNameCtrl, label: 'Primary Contact Name', required: true),
        const SizedBox(height: 16),
        _Field(controller: contactTitleCtrl, label: 'Title / Role'),
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
            if (!RegExp(r'^[^@]+@[^@]+\.[^@]+').hasMatch(v)) return 'Enter a valid email';
            return null;
          },
        ),
        if (isEditing) ...[
          const SizedBox(height: 20),
          Row(
            children: [
              const Text('Active', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
              const Spacer(),
              Switch(value: isActive, onChanged: onActiveChanged, activeColor: AppColors.primary),
            ],
          ),
        ],
      ],
    );
  }
}

class _BillingTab extends StatelessWidget {
  final TextEditingController gstCtrl;
  final String paymentMethod;
  final int netTerms;
  final ValueChanged<String> onPaymentMethodChanged;
  final ValueChanged<int> onNetTermsChanged;

  const _BillingTab({
    required this.gstCtrl,
    required this.paymentMethod,
    required this.netTerms,
    required this.onPaymentMethodChanged,
    required this.onNetTermsChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _Field(controller: gstCtrl, label: 'GST/HST Number (CRA)'),
        const SizedBox(height: 16),
        _DropdownField<String>(
          label: 'Preferred Payment Method',
          value: paymentMethod,
          items: const [
            DropdownMenuItem(value: 'EFT', child: Text('EFT')),
            DropdownMenuItem(value: 'Cheque', child: Text('Cheque')),
            DropdownMenuItem(value: 'Credit Card', child: Text('Credit Card')),
            DropdownMenuItem(value: 'Wire Transfer', child: Text('Wire Transfer')),
          ],
          onChanged: onPaymentMethodChanged,
        ),
        const SizedBox(height: 16),
        _DropdownField<int>(
          label: 'Net Payment Terms',
          value: netTerms,
          items: const [
            DropdownMenuItem(value: 15, child: Text('Net 15')),
            DropdownMenuItem(value: 30, child: Text('Net 30')),
            DropdownMenuItem(value: 45, child: Text('Net 45')),
            DropdownMenuItem(value: 60, child: Text('Net 60')),
          ],
          onChanged: onNetTermsChanged,
        ),
      ],
    );
  }
}

class _AddressTab extends StatelessWidget {
  final TextEditingController streetCtrl;
  final TextEditingController cityCtrl;
  final TextEditingController provinceCtrl;
  final TextEditingController postalCtrl;

  const _AddressTab({
    required this.streetCtrl,
    required this.cityCtrl,
    required this.provinceCtrl,
    required this.postalCtrl,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _Field(controller: streetCtrl, label: 'Street Address', required: true),
        const SizedBox(height: 16),
        _Field(controller: cityCtrl, label: 'City', required: true),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: _Field(
                controller: provinceCtrl,
                label: 'Province',
                required: true,
                maxLength: 2,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  if (v.length != 2) return 'Use 2-letter code (e.g. ON)';
                  return null;
                },
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _Field(
                controller: postalCtrl,
                label: 'Postal Code',
                required: true,
                maxLength: 7,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Required';
                  if (!RegExp(r'^[A-Za-z]\d[A-Za-z] ?\d[A-Za-z]\d$').hasMatch(v.trim())) {
                    return 'Format: A1A 1A1';
                  }
                  return null;
                },
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _ComplianceTab extends StatelessWidget {
  final TextEditingController complianceCtrl;
  final bool isMinesite;
  final ValueChanged<bool> onMineSiteChanged;

  const _ComplianceTab({
    required this.complianceCtrl,
    required this.isMinesite,
    required this.onMineSiteChanged,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Row(
          children: [
            const Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Mine Site Rules Apply (◆)', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                  Text('Enables ◆ indicator on client profile', style: TextStyle(fontSize: 12, color: AppColors.brandGray)),
                ],
              ),
            ),
            Switch(value: isMinesite, onChanged: onMineSiteChanged, activeColor: AppColors.primary),
          ],
        ),
        const SizedBox(height: 16),
        TextFormField(
          controller: complianceCtrl,
          maxLines: 8,
          decoration: InputDecoration(
            labelText: 'Compliance Notes',
            hintText: 'Gate clearance contacts, DVIR requirements, cargo documentation…',
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
            alignLabelWithHint: true,
          ),
        ),
      ],
    );
  }
}

// ─── Shared form widgets ──────────────────────────────────────────────────────

class _Field extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final bool required;
  final TextInputType? keyboardType;
  final String? Function(String?)? validator;
  final int? maxLength;

  const _Field({
    required this.controller,
    required this.label,
    this.required = false,
    this.keyboardType,
    this.validator,
    this.maxLength,
  });

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: keyboardType,
      maxLength: maxLength,
      decoration: InputDecoration(
        labelText: required ? '$label *' : label,
        counterText: '',
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      validator: validator ??
          (required
              ? (v) => (v == null || v.trim().isEmpty) ? '$label is required' : null
              : null),
    );
  }
}

class _DropdownField<T> extends StatelessWidget {
  final String label;
  final T value;
  final List<DropdownMenuItem<T>> items;
  final ValueChanged<T> onChanged;

  const _DropdownField({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<T>(
      value: value,
      decoration: InputDecoration(
        labelText: label,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      items: items,
      onChanged: (v) { if (v != null) onChanged(v); },
    );
  }
}
