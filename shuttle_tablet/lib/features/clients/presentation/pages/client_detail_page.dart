import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';
import '../../domain/entities/contract.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../../domain/repositories/i_contract_repository.dart';
import '../providers/client_detail_provider.dart';
import '../providers/client_form_provider.dart';
import '../providers/clients_provider.dart';
import '../providers/contracts_provider.dart';

const _kAmber = Color(0xFFE8A020);
const _kAmberLight = Color(0xFFFFF8EC);
const _kAmberBorder = Color(0xFFFFE0A0);

class ClientDetailPage extends ConsumerStatefulWidget {
  final String clientId;
  const ClientDetailPage({super.key, required this.clientId});

  @override
  ConsumerState<ClientDetailPage> createState() => _ClientDetailPageState();
}

class _ClientDetailPageState extends ConsumerState<ClientDetailPage>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  bool _isEditing = false;
  bool _isSaving = false;
  bool _populated = false;

  // Form controllers
  late TextEditingController _gstHstCtrl;
  late TextEditingController _contactNameCtrl;
  late TextEditingController _contactTitleCtrl;
  late TextEditingController _phoneCtrl;
  late TextEditingController _emailCtrl;
  late TextEditingController _streetCtrl;
  late TextEditingController _cityCtrl;
  late TextEditingController _postalCtrl;
  late TextEditingController _industryCtrl;
  late TextEditingController _projectSiteCtrl;
  late TextEditingController _complianceCtrl;

  ServiceType _serviceType = ServiceType.corporate;
  int _netPaymentTerms = 30;
  String _preferredPaymentMethod = 'EFT';
  bool _isMinesite = false;
  bool _isActive = true;
  String _province = 'MB';

  List<String> _notificationEmails = [];
  List<String> _tripDepartureArrivalEmails = [];
  List<String> _passengerBookingEmails = [];

  Client? _original;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    _gstHstCtrl = TextEditingController();
    _contactNameCtrl = TextEditingController();
    _contactTitleCtrl = TextEditingController();
    _phoneCtrl = TextEditingController();
    _emailCtrl = TextEditingController();
    _streetCtrl = TextEditingController();
    _cityCtrl = TextEditingController();
    _postalCtrl = TextEditingController();
    _industryCtrl = TextEditingController();
    _projectSiteCtrl = TextEditingController();
    _complianceCtrl = TextEditingController();
  }

  @override
  void dispose() {
    _tabController.dispose();
    for (final c in [
      _gstHstCtrl, _contactNameCtrl, _contactTitleCtrl, _phoneCtrl,
      _emailCtrl, _streetCtrl, _cityCtrl, _postalCtrl,
      _industryCtrl, _projectSiteCtrl, _complianceCtrl,
    ]) {
      c.dispose();
    }
    super.dispose();
  }

  void _populateFromClient(Client client) {
    _original = client;
    _gstHstCtrl.text = client.gstHstNumber ?? '';
    _contactNameCtrl.text = client.primaryContactName;
    _contactTitleCtrl.text = client.primaryContactTitle;
    _phoneCtrl.text = client.phone;
    _emailCtrl.text = client.email;
    _streetCtrl.text = client.streetAddress;
    _cityCtrl.text = client.city;
    _postalCtrl.text = client.postalCode;
    _industryCtrl.text = client.industry ?? '';
    _projectSiteCtrl.text = client.projectSite ?? '';
    _complianceCtrl.text = client.complianceNotes ?? '';
    _serviceType = client.serviceType;
    _netPaymentTerms = client.netPaymentTerms;
    _preferredPaymentMethod = client.preferredPaymentMethod;
    _isMinesite = client.isMinesite;
    _isActive = client.isActive;
    _province = ['MB', 'SK', 'ON', 'AB', 'BC', 'QC'].contains(client.province)
        ? client.province
        : 'MB';
    _notificationEmails = List.from(client.notificationEmails);
    _tripDepartureArrivalEmails = List.from(client.tripDepartureArrivalEmails);
    _passengerBookingEmails = List.from(client.passengerBookingEmails);
    _populated = true;
  }

  Future<void> _saveEdit() async {
    if (_original == null) return;
    setState(() => _isSaving = true);
    try {
      await ref.read(clientFormProvider.notifier).updateClient(
        widget.clientId,
        UpdateClientParams(
          businessName: _original!.businessName,
          serviceType: _serviceType,
          primaryContactName: _contactNameCtrl.text.trim(),
          primaryContactTitle: _contactTitleCtrl.text.trim(),
          phone: _phoneCtrl.text.trim(),
          email: _emailCtrl.text.trim(),
          streetAddress: _streetCtrl.text.trim(),
          city: _cityCtrl.text.trim(),
          province: _province,
          postalCode: _postalCtrl.text.trim().toUpperCase(),
          gstHstNumber:
              _gstHstCtrl.text.trim().isEmpty ? null : _gstHstCtrl.text.trim(),
          preferredPaymentMethod: _preferredPaymentMethod,
          netPaymentTerms: _netPaymentTerms,
          complianceNotes: _complianceCtrl.text.trim().isEmpty
              ? null
              : _complianceCtrl.text.trim(),
          isMinesite: _isMinesite,
          isActive: _isActive,
          industry: _industryCtrl.text.trim().isEmpty
              ? null
              : _industryCtrl.text.trim(),
          projectSite: _projectSiteCtrl.text.trim().isEmpty
              ? null
              : _projectSiteCtrl.text.trim(),
          notificationEmails: _notificationEmails,
          tripDepartureArrivalEmails: _tripDepartureArrivalEmails,
          passengerBookingEmails: _passengerBookingEmails,
        ),
      );
      ref.invalidate(clientDetailProvider(widget.clientId));
      ref.invalidate(clientsProvider);
      if (mounted) setState(() { _isEditing = false; _isSaving = false; _populated = false; });
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to save: $e'), backgroundColor: AppColors.danger),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final clientAsync = ref.watch(clientDetailProvider(widget.clientId));

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          _buildTopBar(context, clientAsync),
          Container(height: 1, color: const Color(0xFFE5E7EB)),
          Expanded(
            child: clientAsync.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline_rounded, size: 48, color: AppColors.brandGray),
                    const SizedBox(height: 12),
                    Text('Error: $e', style: const TextStyle(color: AppColors.brandGray)),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: () => ref.invalidate(clientDetailProvider(widget.clientId)),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (client) {
                if (!_populated) {
                  WidgetsBinding.instance.addPostFrameCallback((_) {
                    if (mounted) setState(() => _populateFromClient(client));
                  });
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _ClientSidebar(
                      client: client,
                      isEditing: _isEditing,
                      industryCtrl: _industryCtrl,
                      projectSiteCtrl: _projectSiteCtrl,
                    ),
                    const VerticalDivider(width: 1, thickness: 1, color: Color(0xFFE5E7EB)),
                    Expanded(child: _buildDetailPane(client)),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTopBar(BuildContext context, AsyncValue<Client> clientAsync) {
    final client = clientAsync.valueOrNull;
    return Container(
      color: Colors.white,
      padding: EdgeInsets.only(
        top: MediaQuery.of(context).padding.top,
        left: 4,
        right: 16,
      ),
      child: Row(
        children: [
          IconButton(
            icon: const Icon(Icons.arrow_back_rounded),
            color: AppColors.textPrimary,
            onPressed: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                  'Client Profile',
                  style: TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827)),
                ),
                if (client != null)
                  Text(
                    client.businessName,
                    style: const TextStyle(fontSize: 12, color: AppColors.brandGray),
                  ),
              ],
            ),
          ),
          if (_isEditing) ...[
            TextButton(
              onPressed: _isSaving
                  ? null
                  : () {
                      if (client != null) {
                        _populateFromClient(client);
                        setState(() => _isEditing = false);
                      }
                    },
              child: const Text('Cancel'),
            ),
            const SizedBox(width: 8),
            FilledButton(
              onPressed: _isSaving ? null : _saveEdit,
              style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
              child: _isSaving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    )
                  : const Text('Save Changes'),
            ),
          ] else ...[
            IconButton(
              icon: const Icon(Icons.edit_rounded),
              color: AppColors.primary,
              tooltip: 'Edit client',
              onPressed: client != null
                  ? () {
                      _populateFromClient(client);
                      setState(() => _isEditing = true);
                    }
                  : null,
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildDetailPane(Client client) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          color: Colors.white,
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Client Operations Profile',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: Color(0xFF111827)),
              ),
              const SizedBox(height: 2),
              const Text(
                'Manage billing, contracts, and operational constraints.',
                style: TextStyle(fontSize: 12, color: AppColors.brandGray),
              ),
              const SizedBox(height: 12),
              TabBar(
                controller: _tabController,
                labelColor: AppColors.primary,
                unselectedLabelColor: AppColors.brandGray,
                indicatorColor: AppColors.primary,
                isScrollable: true,
                tabAlignment: TabAlignment.start,
                dividerColor: Colors.transparent,
                tabs: const [
                  Tab(text: 'Overview & Billing'),
                  Tab(text: 'Documents'),
                  Tab(text: 'Communication Log'),
                  Tab(text: 'Trip History'),
                ],
              ),
            ],
          ),
        ),
        Container(height: 1, color: const Color(0xFFE5E7EB)),
        Expanded(
          child: TabBarView(
            controller: _tabController,
            children: [
              _buildOverviewTab(client),
              _buildPlaceholderTab(Icons.folder_open_outlined, 'Documents coming soon',
                  subtitle: '7-year retention per CRA requirements'),
              _buildPlaceholderTab(Icons.chat_bubble_outline_rounded, 'Communication log coming soon'),
              _buildPlaceholderTab(Icons.route_outlined, 'Trip history coming soon'),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildOverviewTab(Client client) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Left column
          Expanded(
            child: Column(
              children: [
                _buildContactCard(client),
                const SizedBox(height: 16),
                _buildNotificationEmailsCard(client),
                const SizedBox(height: 16),
                _buildConstraintsCard(client),
              ],
            ),
          ),
          const SizedBox(width: 16),
          // Right column
          Expanded(
            child: Column(
              children: [
                _buildBillingCard(client),
                const SizedBox(height: 16),
                _ContractSection(clientId: widget.clientId),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBillingCard(Client client) {
    return _SectionCard(
      title: 'Billing Settings',
      child: Column(
        children: [
          _buildInfoOrField(
            label: 'GST/HST Number',
            value: client.gstHstNumber?.isEmpty ?? true ? '—' : (client.gstHstNumber ?? '—'),
            isEditing: _isEditing,
            editWidget: TextField(
              controller: _gstHstCtrl,
              style: const TextStyle(fontFamily: 'monospace'),
              decoration: _inputDec('GST/HST Number (CRA)'),
            ),
          ),
          const SizedBox(height: 12),
          _buildInfoOrField(
            label: 'Preferred Payment Method',
            value: client.preferredPaymentMethod,
            isEditing: _isEditing,
            editWidget: DropdownButtonFormField<String>(
              value: _preferredPaymentMethod,
              decoration: _inputDec('Payment Method'),
              items: const [
                DropdownMenuItem(value: 'EFT', child: Text('EFT')),
                DropdownMenuItem(value: 'Cheque', child: Text('Cheque')),
                DropdownMenuItem(value: 'Credit Card', child: Text('Credit Card')),
                DropdownMenuItem(value: 'Wire Transfer', child: Text('Wire Transfer')),
              ],
              onChanged: (v) { if (v != null) setState(() => _preferredPaymentMethod = v); },
            ),
          ),
          const SizedBox(height: 12),
          _buildInfoOrField(
            label: 'Net Payment Terms',
            value: 'Net ${client.netPaymentTerms}',
            isEditing: _isEditing,
            editWidget: DropdownButtonFormField<int>(
              value: _netPaymentTerms,
              decoration: _inputDec('Net Payment Terms'),
              items: const [
                DropdownMenuItem(value: 15, child: Text('Net 15')),
                DropdownMenuItem(value: 30, child: Text('Net 30')),
                DropdownMenuItem(value: 45, child: Text('Net 45')),
                DropdownMenuItem(value: 60, child: Text('Net 60')),
              ],
              onChanged: (v) { if (v != null) setState(() => _netPaymentTerms = v); },
            ),
          ),
          if (_isEditing) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                const Text('Active', style: TextStyle(fontSize: 13, color: AppColors.textSecondary)),
                const Spacer(),
                Switch(
                  value: _isActive,
                  onChanged: (v) => setState(() => _isActive = v),
                  activeColor: AppColors.primary,
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildContactCard(Client client) {
    final initials = client.primaryContactName.isNotEmpty
        ? client.primaryContactName.trim().split(' ').take(2).map((w) => w[0].toUpperCase()).join()
        : '?';

    return _SectionCard(
      title: 'Primary Contact',
      child: Column(
        children: [
          if (!_isEditing) ...[
            Row(
              children: [
                CircleAvatar(
                  radius: 24,
                  backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                  child: Text(initials,
                      style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.primary,
                          fontSize: 16)),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(client.primaryContactName,
                          style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: 14,
                              color: Color(0xFF111827))),
                      if (client.primaryContactTitle.isNotEmpty)
                        Text(client.primaryContactTitle,
                            style: const TextStyle(
                                fontSize: 12, color: AppColors.brandGray)),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            _ContactRow(icon: Icons.phone_outlined, label: 'Phone', value: client.phone),
            const SizedBox(height: 8),
            _ContactRow(icon: Icons.email_outlined, label: 'Email', value: client.email),
            const SizedBox(height: 8),
            _ContactRow(
              icon: Icons.location_on_outlined,
              label: 'Address',
              value: '${client.streetAddress}, ${client.city}, ${client.province} ${client.postalCode}',
            ),
          ] else ...[
            TextField(
              controller: _contactNameCtrl,
              decoration: _inputDec('Contact Name *'),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _contactTitleCtrl,
              decoration: _inputDec('Title / Role'),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _phoneCtrl,
              keyboardType: TextInputType.phone,
              decoration: _inputDec('Phone Number *'),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _emailCtrl,
              keyboardType: TextInputType.emailAddress,
              decoration: _inputDec('Email Address *'),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _streetCtrl,
              decoration: _inputDec('Street Address *'),
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _cityCtrl,
                    decoration: _inputDec('City *'),
                  ),
                ),
                const SizedBox(width: 10),
                SizedBox(
                  width: 120,
                  child: DropdownButtonFormField<String>(
                    value: _province,
                    decoration: _inputDec('Province'),
                    items: const [
                      DropdownMenuItem(value: 'MB', child: Text('MB')),
                      DropdownMenuItem(value: 'SK', child: Text('SK')),
                      DropdownMenuItem(value: 'ON', child: Text('ON')),
                      DropdownMenuItem(value: 'AB', child: Text('AB')),
                      DropdownMenuItem(value: 'BC', child: Text('BC')),
                      DropdownMenuItem(value: 'QC', child: Text('QC')),
                    ],
                    onChanged: (v) { if (v != null) setState(() => _province = v); },
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            TextField(
              controller: _postalCtrl,
              textCapitalization: TextCapitalization.characters,
              decoration: _inputDec('Postal Code *'),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildNotificationEmailsCard(Client client) {
    return _SectionCard(
      title: 'Notification Emails',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Configure who receives automated emails for this client.',
            style: TextStyle(fontSize: 12, color: AppColors.brandGray),
          ),
          const SizedBox(height: 14),
          _NotificationEmailGroup(
            title: 'General Notifications',
            subtitle: 'Operational alerts and account updates',
            emails: _isEditing ? _notificationEmails : client.notificationEmails,
            isEditing: _isEditing,
            onChanged: (emails) => setState(() => _notificationEmails = emails),
          ),
          const SizedBox(height: 14),
          _NotificationEmailGroup(
            title: 'Trip Departures & Arrivals',
            subtitle: 'Updates when trips depart or arrive',
            emails: _isEditing
                ? _tripDepartureArrivalEmails
                : client.tripDepartureArrivalEmails,
            isEditing: _isEditing,
            onChanged: (emails) =>
                setState(() => _tripDepartureArrivalEmails = emails),
          ),
          const SizedBox(height: 14),
          _NotificationEmailGroup(
            title: 'Passenger Booking Alerts',
            subtitle: 'Notified when passengers are booked on trips',
            emails: _isEditing
                ? _passengerBookingEmails
                : client.passengerBookingEmails,
            isEditing: _isEditing,
            onChanged: (emails) =>
                setState(() => _passengerBookingEmails = emails),
          ),
        ],
      ),
    );
  }

  Widget _buildConstraintsCard(Client client) {
    final lines = (client.complianceNotes ?? '')
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList();

    if (!_isEditing && !client.isMinesite && lines.isEmpty) {
      return const SizedBox.shrink();
    }

    return Container(
      decoration: BoxDecoration(
        color: _kAmberLight,
        border: Border.all(color: _kAmberBorder),
        borderRadius: BorderRadius.circular(16),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.diamond_outlined, color: _kAmber, size: 16),
              const SizedBox(width: 6),
              const Text(
                'OPERATIONAL CONSTRAINTS',
                style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: _kAmber,
                    letterSpacing: 0.8),
              ),
            ],
          ),
          const SizedBox(height: 10),
          if (_isEditing) ...[
            Row(
              children: [
                const Expanded(
                  child: Text('Mine Site Rules Apply',
                      style: TextStyle(fontSize: 13, color: AppColors.textPrimary)),
                ),
                Switch(
                  value: _isMinesite,
                  onChanged: (v) => setState(() => _isMinesite = v),
                  activeColor: _kAmber,
                ),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _complianceCtrl,
              maxLines: 5,
              decoration: _inputDec('Compliance notes (one per line)'),
            ),
          ] else ...[
            Container(
              decoration: BoxDecoration(
                color: Colors.white,
                border: Border.all(color: _kAmberBorder),
                borderRadius: BorderRadius.circular(10),
              ),
              padding: const EdgeInsets.all(12),
              child: lines.isEmpty
                  ? const Text('No constraints recorded.',
                      style: TextStyle(fontSize: 13, color: AppColors.brandGray))
                  : Column(
                      children: lines
                          .map((line) => Padding(
                                padding: const EdgeInsets.only(bottom: 6),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Padding(
                                      padding: EdgeInsets.only(top: 3),
                                      child: Icon(Icons.check, color: _kAmber, size: 13),
                                    ),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(line,
                                          style: const TextStyle(
                                              fontSize: 13,
                                              color: Color(0xFF374151))),
                                    ),
                                  ],
                                ),
                              ))
                          .toList(),
                    ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildPlaceholderTab(IconData icon, String message, {String? subtitle}) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(icon, size: 48, color: AppColors.brandGray),
          const SizedBox(height: 12),
          Text(message, style: const TextStyle(color: AppColors.brandGray)),
          if (subtitle != null) ...[
            const SizedBox(height: 6),
            Text(subtitle,
                style: const TextStyle(fontSize: 11, color: AppColors.brandGray)),
          ],
        ],
      ),
    );
  }

  Widget _buildInfoOrField({
    required String label,
    required String value,
    required bool isEditing,
    required Widget editWidget,
  }) {
    if (isEditing) return editWidget;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label.toUpperCase(),
            style: const TextStyle(
                fontSize: 10,
                fontWeight: FontWeight.w700,
                color: AppColors.brandGray,
                letterSpacing: 0.6)),
        const SizedBox(height: 2),
        Text(value,
            style: const TextStyle(fontSize: 14, color: Color(0xFF111827))),
      ],
    );
  }

  InputDecoration _inputDec(String label) => InputDecoration(
        labelText: label,
        isDense: true,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      );
}

// ─── Sidebar ──────────────────────────────────────────────────────────────────

class _ClientSidebar extends StatelessWidget {
  final Client client;
  final bool isEditing;
  final TextEditingController industryCtrl;
  final TextEditingController projectSiteCtrl;

  const _ClientSidebar({
    required this.client,
    required this.isEditing,
    required this.industryCtrl,
    required this.projectSiteCtrl,
  });

  @override
  Widget build(BuildContext context) {
    final initials = client.businessName.isNotEmpty
        ? client.businessName.trim().split(' ').take(2).map((w) => w[0].toUpperCase()).join()
        : '?';

    final isCorporate = client.serviceType == ServiceType.corporate;
    final badgeColor = isCorporate ? _kAmber : AppColors.secondary;
    final badgeBg = isCorporate ? _kAmberLight : AppColors.secondary.withValues(alpha: 0.1);

    return Container(
      width: 260,
      color: Colors.white,
      padding: const EdgeInsets.all(20),
      child: Column(
        children: [
          CircleAvatar(
            radius: 40,
            backgroundColor: AppColors.primary.withValues(alpha: 0.1),
            child: Text(
              initials,
              style: const TextStyle(
                  fontSize: 26,
                  fontWeight: FontWeight.w700,
                  color: AppColors.primary),
            ),
          ),
          const SizedBox(height: 14),
          Text(
            client.businessName,
            style: const TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w700,
                color: Color(0xFF111827)),
            textAlign: TextAlign.center,
          ),
          if (!isEditing && (client.projectSite?.isNotEmpty ?? false)) ...[
            const SizedBox(height: 4),
            Text(
              client.projectSite!,
              style: const TextStyle(fontSize: 13, color: AppColors.brandGray),
              textAlign: TextAlign.center,
            ),
          ],
          if (isEditing) ...[
            const SizedBox(height: 10),
            TextField(
              controller: projectSiteCtrl,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 13),
              decoration: InputDecoration(
                hintText: 'Project / Site (optional)',
                isDense: true,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
            decoration: BoxDecoration(
              color: badgeBg,
              border: Border.all(color: badgeColor.withValues(alpha: 0.35)),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              isCorporate ? 'Corporate' : 'Community',
              style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: badgeColor,
                  letterSpacing: 0.5),
            ),
          ),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 12),
          _SidebarInfoRow(
            label: 'Industry',
            value: client.industry?.isEmpty ?? true ? '—' : (client.industry ?? '—'),
            isEditing: isEditing,
            editWidget: TextField(
              controller: industryCtrl,
              decoration: InputDecoration(
                hintText: 'e.g. Mining & Resources',
                isDense: true,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              ),
            ),
          ),
          const SizedBox(height: 14),
          _SidebarInfoRow(
            label: 'Client Since',
            value: DateFormat('MMMM yyyy').format(client.createdAt),
          ),
          const SizedBox(height: 14),
          _SidebarInfoRow(
            label: 'Status',
            valueWidget: Row(
              children: [
                Container(
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: client.isActive ? AppColors.success : AppColors.danger,
                  ),
                ),
                const SizedBox(width: 6),
                Text(
                  client.isActive ? 'Active' : 'Inactive',
                  style: const TextStyle(fontSize: 14, color: Color(0xFF111827)),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SidebarInfoRow extends StatelessWidget {
  final String label;
  final String? value;
  final Widget? valueWidget;
  final bool isEditing;
  final Widget? editWidget;

  const _SidebarInfoRow({
    required this.label,
    this.value,
    this.valueWidget,
    this.isEditing = false,
    this.editWidget,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label.toUpperCase(),
          style: const TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: AppColors.brandGray,
              letterSpacing: 0.6),
        ),
        const SizedBox(height: 4),
        if (isEditing && editWidget != null)
          editWidget!
        else if (valueWidget != null)
          valueWidget!
        else
          Text(
            value ?? '—',
            style: const TextStyle(fontSize: 14, color: Color(0xFF111827)),
          ),
      ],
    );
  }
}

// ─── Contract Section ─────────────────────────────────────────────────────────

class _ContractSection extends ConsumerStatefulWidget {
  final String clientId;
  const _ContractSection({required this.clientId});

  @override
  ConsumerState<_ContractSection> createState() => _ContractSectionState();
}

class _ContractSectionState extends ConsumerState<_ContractSection> {
  Future<void> _showAddContractDialog() async {
    DateTime startDate = DateTime.now();
    DateTime renewalDate = DateTime.now().add(const Duration(days: 365));
    final notesCtrl = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Add Contract', style: TextStyle(fontWeight: FontWeight.w700)),
          content: SizedBox(
            width: 400,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Start Date', style: TextStyle(fontSize: 13, color: AppColors.brandGray)),
                  subtitle: Text(DateFormat('MMM d, yyyy').format(startDate),
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
                  trailing: const Icon(Icons.calendar_today_outlined, size: 18),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: ctx,
                      initialDate: startDate,
                      firstDate: DateTime(2020),
                      lastDate: DateTime(2040),
                    );
                    if (picked != null) setDialogState(() => startDate = picked);
                  },
                ),
                const Divider(),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Renewal Date', style: TextStyle(fontSize: 13, color: AppColors.brandGray)),
                  subtitle: Text(DateFormat('MMM d, yyyy').format(renewalDate),
                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
                  trailing: const Icon(Icons.calendar_today_outlined, size: 18),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: ctx,
                      initialDate: renewalDate,
                      firstDate: DateTime(2020),
                      lastDate: DateTime(2040),
                    );
                    if (picked != null) setDialogState(() => renewalDate = picked);
                  },
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: notesCtrl,
                  maxLines: 3,
                  decoration: InputDecoration(
                    labelText: 'Notes (optional)',
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    isDense: true,
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
              child: const Text('Add Contract'),
            ),
          ],
        ),
      ),
    );

    if (confirmed == true && mounted) {
      try {
        await ref.read(contractsProvider(widget.clientId).notifier).createContract(
          CreateContractParams(
            clientId: widget.clientId,
            startDate: startDate,
            renewalDate: renewalDate,
            notes: notesCtrl.text.trim().isEmpty ? null : notesCtrl.text.trim(),
            rateLines: const [],
          ),
        );
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Failed to add contract: $e'), backgroundColor: AppColors.danger),
          );
        }
      }
    }
    notesCtrl.dispose();
  }

  Future<void> _showAddRateLineDialog(String contractId) async {
    final billingCodeCtrl = TextEditingController();
    final descriptionCtrl = TextEditingController();
    final vehicleTypeCtrl = TextEditingController();
    final maxDistCtrl = TextEditingController();
    final dayRateCtrl = TextEditingController();
    bool cargoIncluded = false;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Add Rate Line', style: TextStyle(fontWeight: FontWeight.w700)),
          content: SizedBox(
            width: 400,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: billingCodeCtrl,
                    decoration: InputDecoration(
                        labelText: 'Billing Code *',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        isDense: true),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: descriptionCtrl,
                    decoration: InputDecoration(
                        labelText: 'Description *',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        isDense: true),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: vehicleTypeCtrl,
                    decoration: InputDecoration(
                        labelText: 'Vehicle Type *',
                        hintText: 'e.g. Van, Truck',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        isDense: true),
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: maxDistCtrl,
                          keyboardType: TextInputType.number,
                          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
                          decoration: InputDecoration(
                              labelText: 'Max Dist (km)',
                              border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                              isDense: true),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: TextField(
                          controller: dayRateCtrl,
                          keyboardType: const TextInputType.numberWithOptions(decimal: true),
                          decoration: InputDecoration(
                              labelText: 'Day Rate (\$) *',
                              border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                              isDense: true),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Checkbox(
                        value: cargoIncluded,
                        onChanged: (v) => setDialogState(() => cargoIncluded = v ?? false),
                        activeColor: AppColors.primary,
                      ),
                      const Text('Cargo Included', style: TextStyle(fontSize: 14)),
                    ],
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
              child: const Text('Add Rate Line'),
            ),
          ],
        ),
      ),
    );

    if (confirmed == true && mounted) {
      final billingCode = billingCodeCtrl.text.trim();
      final description = descriptionCtrl.text.trim();
      final vehicleType = vehicleTypeCtrl.text.trim();
      final dayRate = double.tryParse(dayRateCtrl.text.trim()) ?? 0;

      if (billingCode.isEmpty || description.isEmpty || vehicleType.isEmpty || dayRate <= 0) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Please fill all required fields.'), backgroundColor: AppColors.danger),
        );
      } else {
        try {
          await ref.read(contractsProvider(widget.clientId).notifier).addRateLine(
            AddRateLineParams(
              contractId: contractId,
              clientId: widget.clientId,
              billingCode: billingCode,
              description: description,
              vehicleType: vehicleType,
              maxDistanceKm: maxDistCtrl.text.trim().isEmpty
                  ? null
                  : int.tryParse(maxDistCtrl.text.trim()),
              cargoIncluded: cargoIncluded,
              dayRate: dayRate,
            ),
          );
        } catch (e) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Failed to add rate line: $e'), backgroundColor: AppColors.danger),
            );
          }
        }
      }
    }

    billingCodeCtrl.dispose();
    descriptionCtrl.dispose();
    vehicleTypeCtrl.dispose();
    maxDistCtrl.dispose();
    dayRateCtrl.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final contractsAsync = ref.watch(contractsProvider(widget.clientId));

    return _SectionCard(
      title: 'Contracts',
      trailing: TextButton.icon(
        onPressed: _showAddContractDialog,
        icon: const Icon(Icons.add_rounded, size: 16),
        label: const Text('Add Contract'),
        style: TextButton.styleFrom(foregroundColor: AppColors.primary, padding: EdgeInsets.zero),
      ),
      child: contractsAsync.when(
        loading: () => const Padding(
          padding: EdgeInsets.all(16),
          child: Center(child: CircularProgressIndicator()),
        ),
        error: (e, _) => Padding(
          padding: const EdgeInsets.all(8),
          child: Text('Error loading contracts: $e',
              style: const TextStyle(color: AppColors.danger, fontSize: 13)),
        ),
        data: (contracts) {
          if (contracts.isEmpty) {
            return const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Center(
                child: Text('No contracts on file.',
                    style: TextStyle(color: AppColors.brandGray, fontSize: 13)),
              ),
            );
          }
          return Column(
            children: contracts
                .map((contract) => _ContractCard(
                      contract: contract,
                      clientId: widget.clientId,
                      onAddRateLine: () => _showAddRateLineDialog(contract.id),
                      onDeleteRateLine: (rateLineId) async {
                        try {
                          await ref
                              .read(contractsProvider(widget.clientId).notifier)
                              .deleteRateLine(rateLineId, widget.clientId);
                        } catch (e) {
                          if (context.mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                  content: Text('Failed to delete: $e'),
                                  backgroundColor: AppColors.danger),
                            );
                          }
                        }
                      },
                    ))
                .toList(),
          );
        },
      ),
    );
  }
}

class _ContractCard extends StatefulWidget {
  final Contract contract;
  final String clientId;
  final VoidCallback onAddRateLine;
  final Future<void> Function(String rateLineId) onDeleteRateLine;

  const _ContractCard({
    required this.contract,
    required this.clientId,
    required this.onAddRateLine,
    required this.onDeleteRateLine,
  });

  @override
  State<_ContractCard> createState() => _ContractCardState();
}

class _ContractCardState extends State<_ContractCard> {
  bool _expanded = true;

  @override
  Widget build(BuildContext context) {
    final c = widget.contract;
    final fmt = DateFormat('MMM d, yyyy');

    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        border: Border.all(
            color: c.isExpiringSoon ? _kAmberBorder : const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(12),
        color: c.isExpiringSoon ? _kAmberLight : const Color(0xFFF9FAFB),
      ),
      child: Column(
        children: [
          // Contract header
          InkWell(
            borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
            onTap: () => setState(() => _expanded = !_expanded),
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                              decoration: BoxDecoration(
                                color: c.isActive
                                    ? AppColors.success.withValues(alpha: 0.12)
                                    : AppColors.brandGray.withValues(alpha: 0.12),
                                borderRadius: BorderRadius.circular(12),
                              ),
                              child: Text(
                                c.isActive ? 'Active' : 'Inactive',
                                style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.w600,
                                    color: c.isActive ? AppColors.success : AppColors.brandGray),
                              ),
                            ),
                            if (c.isExpiringSoon) ...[
                              const SizedBox(width: 6),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                decoration: BoxDecoration(
                                  color: _kAmberLight,
                                  border: Border.all(color: _kAmberBorder),
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: const Text(
                                  'Expiring Soon',
                                  style: TextStyle(
                                      fontSize: 11,
                                      fontWeight: FontWeight.w600,
                                      color: _kAmber),
                                ),
                              ),
                            ],
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          '${fmt.format(c.startDate)} → ${fmt.format(c.renewalDate)}',
                          style: const TextStyle(fontSize: 12, color: AppColors.brandGray),
                        ),
                        if (c.notes?.isNotEmpty ?? false)
                          Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(c.notes!,
                                style: const TextStyle(fontSize: 12, color: AppColors.textSecondary),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis),
                          ),
                      ],
                    ),
                  ),
                  Icon(
                    _expanded ? Icons.expand_less_rounded : Icons.expand_more_rounded,
                    color: AppColors.brandGray,
                  ),
                ],
              ),
            ),
          ),
          if (_expanded) ...[
            const Divider(height: 1, color: Color(0xFFE5E7EB)),
            // Rate lines table
            if (c.rateLines.isEmpty)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Row(
                  children: [
                    const Expanded(
                      child: Text('No rate lines.',
                          style: TextStyle(fontSize: 12, color: AppColors.brandGray)),
                    ),
                    TextButton.icon(
                      onPressed: widget.onAddRateLine,
                      icon: const Icon(Icons.add_rounded, size: 14),
                      label: const Text('Add Rate Line', style: TextStyle(fontSize: 12)),
                      style: TextButton.styleFrom(
                          foregroundColor: AppColors.primary,
                          padding: const EdgeInsets.symmetric(horizontal: 8)),
                    ),
                  ],
                ),
              )
            else ...[
              // Table header
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                child: Row(
                  children: const [
                    Expanded(flex: 2, child: _TableHeader('Code')),
                    Expanded(flex: 3, child: _TableHeader('Description')),
                    Expanded(flex: 2, child: _TableHeader('Vehicle')),
                    Expanded(flex: 2, child: _TableHeader('Max Km')),
                    Expanded(flex: 2, child: _TableHeader('Cargo')),
                    Expanded(flex: 2, child: _TableHeader('Rate/Day')),
                    SizedBox(width: 36),
                  ],
                ),
              ),
              const Divider(height: 1, color: Color(0xFFE5E7EB)),
              ...c.rateLines.map((rl) => Column(
                    children: [
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                        child: Row(
                          children: [
                            Expanded(flex: 2, child: _TableCell(rl.billingCode)),
                            Expanded(flex: 3, child: _TableCell(rl.description)),
                            Expanded(flex: 2, child: _TableCell(rl.vehicleType)),
                            Expanded(
                                flex: 2,
                                child: _TableCell(rl.maxDistanceKm != null
                                    ? '${rl.maxDistanceKm} km'
                                    : '—')),
                            Expanded(
                                flex: 2,
                                child: _TableCell(rl.cargoIncluded ? 'Yes' : 'No')),
                            Expanded(
                                flex: 2,
                                child: _TableCell(
                                    '\$${rl.dayRate.toStringAsFixed(0)}')),
                            SizedBox(
                              width: 36,
                              child: IconButton(
                                icon: const Icon(Icons.delete_outline_rounded,
                                    size: 18),
                                color: AppColors.danger,
                                padding: EdgeInsets.zero,
                                onPressed: () async {
                                  final confirmed = await showDialog<bool>(
                                    context: context,
                                    builder: (ctx) => AlertDialog(
                                      title: const Text('Delete Rate Line'),
                                      content: Text(
                                          'Remove "${rl.billingCode} — ${rl.description}"?'),
                                      actions: [
                                        TextButton(
                                            onPressed: () =>
                                                Navigator.pop(ctx, false),
                                            child: const Text('Cancel')),
                                        FilledButton(
                                          onPressed: () =>
                                              Navigator.pop(ctx, true),
                                          style: FilledButton.styleFrom(
                                              backgroundColor: AppColors.danger),
                                          child: const Text('Delete'),
                                        ),
                                      ],
                                    ),
                                  );
                                  if (confirmed == true) {
                                    await widget.onDeleteRateLine(rl.id);
                                  }
                                },
                              ),
                            ),
                          ],
                        ),
                      ),
                      const Divider(height: 1, color: Color(0xFFE5E7EB)),
                    ],
                  )),
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
                child: Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    onPressed: widget.onAddRateLine,
                    icon: const Icon(Icons.add_rounded, size: 14),
                    label: const Text('Add Rate Line', style: TextStyle(fontSize: 12)),
                    style: TextButton.styleFrom(
                        foregroundColor: AppColors.primary,
                        padding: const EdgeInsets.symmetric(horizontal: 8)),
                  ),
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }
}

class _NotificationEmailGroup extends StatefulWidget {
  final String title;
  final String subtitle;
  final List<String> emails;
  final bool isEditing;
  final ValueChanged<List<String>> onChanged;

  const _NotificationEmailGroup({
    required this.title,
    required this.subtitle,
    required this.emails,
    required this.isEditing,
    required this.onChanged,
  });

  @override
  State<_NotificationEmailGroup> createState() => _NotificationEmailGroupState();
}

class _NotificationEmailGroupState extends State<_NotificationEmailGroup> {
  final _inputCtrl = TextEditingController();

  @override
  void dispose() {
    _inputCtrl.dispose();
    super.dispose();
  }

  bool _isValidEmail(String value) {
    return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value);
  }

  void _addEmail() {
    final email = _inputCtrl.text.trim();
    if (email.isEmpty) return;
    if (!_isValidEmail(email)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Enter a valid email address'),
          backgroundColor: AppColors.danger,
        ),
      );
      return;
    }
    if (widget.emails.any((e) => e.toLowerCase() == email.toLowerCase())) {
      _inputCtrl.clear();
      return;
    }
    widget.onChanged([...widget.emails, email]);
    _inputCtrl.clear();
  }

  void _removeEmail(String email) {
    widget.onChanged(widget.emails.where((e) => e != email).toList());
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          widget.title.toUpperCase(),
          style: const TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.w700,
            color: AppColors.brandGray,
            letterSpacing: 0.6,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          widget.subtitle,
          style: const TextStyle(fontSize: 11, color: AppColors.brandGray),
        ),
        const SizedBox(height: 8),
        if (widget.emails.isEmpty && !widget.isEditing)
          const Text(
            'No emails configured',
            style: TextStyle(fontSize: 13, color: AppColors.brandGray),
          )
        else if (widget.emails.isNotEmpty)
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: widget.emails
                .map(
                  (email) => InputChip(
                    label: Text(email),
                    deleteIcon: widget.isEditing
                        ? const Icon(Icons.close_rounded, size: 16)
                        : null,
                    onDeleted:
                        widget.isEditing ? () => _removeEmail(email) : null,
                    backgroundColor: const Color(0xFFF3F4F6),
                    side: const BorderSide(color: Color(0xFFE5E7EB)),
                  ),
                )
                .toList(),
          ),
        if (widget.isEditing) ...[
          if (widget.emails.isNotEmpty) const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _inputCtrl,
                  keyboardType: TextInputType.emailAddress,
                  decoration: InputDecoration(
                    hintText: 'Add email address',
                    isDense: true,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 10,
                    ),
                  ),
                  onSubmitted: (_) => _addEmail(),
                ),
              ),
              const SizedBox(width: 8),
              IconButton.filled(
                onPressed: _addEmail,
                icon: const Icon(Icons.add_rounded, size: 18),
                style: IconButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }
}

// ─── Shared UI helpers ────────────────────────────────────────────────────────

class _SectionCard extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;

  const _SectionCard({required this.title, required this.child, this.trailing});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [
          BoxShadow(color: Color(0x06000000), blurRadius: 12, offset: Offset(0, 4)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 12, 10),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                        color: Color(0xFF111827)),
                  ),
                ),
                if (trailing != null) trailing!,
              ],
            ),
          ),
          const Divider(height: 1, color: Color(0xFFF3F4F6)),
          Padding(padding: const EdgeInsets.all(16), child: child),
        ],
      ),
    );
  }
}

class _ContactRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _ContactRow({required this.icon, required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 32,
          height: 32,
          decoration: BoxDecoration(
            color: const Color(0xFFF9FAFB),
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: const Color(0xFFE5E7EB)),
          ),
          child: Icon(icon, size: 15, color: AppColors.brandGray),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label,
                  style: const TextStyle(fontSize: 11, color: AppColors.brandGray)),
              Text(value,
                  style: const TextStyle(fontSize: 13, color: Color(0xFF111827))),
            ],
          ),
        ),
      ],
    );
  }
}

class _TableHeader extends StatelessWidget {
  final String text;
  const _TableHeader(this.text);

  @override
  Widget build(BuildContext context) => Text(
        text.toUpperCase(),
        style: const TextStyle(
            fontSize: 10,
            fontWeight: FontWeight.w700,
            color: AppColors.brandGray,
            letterSpacing: 0.4),
      );
}

class _TableCell extends StatelessWidget {
  final String text;
  const _TableCell(this.text);

  @override
  Widget build(BuildContext context) => Text(
        text,
        style: const TextStyle(fontSize: 13, color: Color(0xFF374151)),
        overflow: TextOverflow.ellipsis,
      );
}
