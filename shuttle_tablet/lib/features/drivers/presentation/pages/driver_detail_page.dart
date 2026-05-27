import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/driver.dart';
import '../../domain/entities/driver_document.dart';
import '../../domain/repositories/i_driver_repository.dart';
import '../providers/driver_detail_provider.dart';
import '../providers/driver_documents_provider.dart';
import '../providers/driver_form_provider.dart';
import '../providers/drivers_provider.dart';

class DriverDetailPage extends ConsumerStatefulWidget {
  final String driverId;
  const DriverDetailPage({super.key, required this.driverId});

  @override
  ConsumerState<DriverDetailPage> createState() => _DriverDetailPageState();
}

class _DriverDetailPageState extends ConsumerState<DriverDetailPage>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  bool _isEditing = false;
  bool _isSaving = false;
  bool _populated = false;

  // Edit-mode controllers
  late TextEditingController _firstNameCtrl;
  late TextEditingController _lastNameCtrl;
  late TextEditingController _phoneCtrl;
  late TextEditingController _emailCtrl;
  late TextEditingController _employeeIdCtrl;
  DateTime _hireDate = DateTime.now();
  bool _isActive = true;

  Driver? _original;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _firstNameCtrl = TextEditingController();
    _lastNameCtrl = TextEditingController();
    _phoneCtrl = TextEditingController();
    _emailCtrl = TextEditingController();
    _employeeIdCtrl = TextEditingController();
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

  void _populateFromDriver(Driver driver) {
    _original = driver;
    _firstNameCtrl.text = driver.firstName;
    _lastNameCtrl.text = driver.lastName;
    _phoneCtrl.text = driver.phone;
    _emailCtrl.text = driver.email;
    _employeeIdCtrl.text = driver.employeeId;
    _hireDate = driver.hireDate;
    _isActive = driver.isActive;
    _populated = true;
  }

  Future<void> _saveEdit() async {
    if (_original == null) return;
    setState(() => _isSaving = true);
    try {
      await ref.read(driverFormProvider.notifier).updateDriver(
            widget.driverId,
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
      ref.invalidate(driverDetailProvider(widget.driverId));
      ref.invalidate(driversProvider);
      if (mounted) {
        setState(() {
          _isEditing = false;
          _isSaving = false;
          _populated = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Failed to save: $e'),
              backgroundColor: AppColors.danger),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final driverAsync = ref.watch(driverDetailProvider(widget.driverId));

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          _buildTopBar(context, driverAsync),
          Container(height: 1, color: const Color(0xFFE5E7EB)),
          Expanded(
            child: driverAsync.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        size: 48, color: AppColors.brandGray),
                    const SizedBox(height: 12),
                    Text('Error: $e',
                        style: const TextStyle(
                            color: AppColors.brandGray)),
                    const SizedBox(height: 12),
                    FilledButton(
                      onPressed: () => ref.invalidate(
                          driverDetailProvider(widget.driverId)),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (driver) {
                if (!_populated) {
                  WidgetsBinding.instance.addPostFrameCallback((_) {
                    if (mounted) {
                      setState(() => _populateFromDriver(driver));
                    }
                  });
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _DriverSidebar(
                      driver: driver,
                      isEditing: _isEditing,
                      firstNameCtrl: _firstNameCtrl,
                      lastNameCtrl: _lastNameCtrl,
                      phoneCtrl: _phoneCtrl,
                      emailCtrl: _emailCtrl,
                      employeeIdCtrl: _employeeIdCtrl,
                      hireDate: _hireDate,
                      isActive: _isActive,
                      onHireDateChanged: (d) =>
                          setState(() => _hireDate = d),
                      onActiveChanged: (v) =>
                          setState(() => _isActive = v),
                    ),
                    const VerticalDivider(
                        width: 1,
                        thickness: 1,
                        color: Color(0xFFE5E7EB)),
                    Expanded(child: _buildDetailPane(driver)),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTopBar(
      BuildContext context, AsyncValue<Driver> driverAsync) {
    final driver = driverAsync.valueOrNull;
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
                  'Driver Profile',
                  style: TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827)),
                ),
                if (driver != null)
                  Text(
                    driver.fullName,
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.brandGray),
                  ),
              ],
            ),
          ),
          if (_isEditing) ...[
            TextButton(
              onPressed: _isSaving
                  ? null
                  : () {
                      if (driver != null) {
                        _populateFromDriver(driver);
                        setState(() => _isEditing = false);
                      }
                    },
              child: const Text('Cancel'),
            ),
            const SizedBox(width: 8),
            FilledButton(
              onPressed: _isSaving ? null : _saveEdit,
              style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary),
              child: _isSaving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white))
                  : const Text('Save Changes'),
            ),
          ] else ...[
            IconButton(
              icon: const Icon(Icons.edit_rounded),
              color: AppColors.primary,
              tooltip: 'Edit driver',
              onPressed: driver != null
                  ? () {
                      _populateFromDriver(driver);
                      setState(() => _isEditing = true);
                    }
                  : null,
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildDetailPane(Driver driver) {
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
                'Driver Operations Profile',
                style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827)),
              ),
              const SizedBox(height: 2),
              const Text(
                'Manage compliance documents and scheduling.',
                style:
                    TextStyle(fontSize: 12, color: AppColors.brandGray),
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
                  Tab(text: 'Overview'),
                  Tab(text: 'Documents'),
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
              _buildOverviewTab(driver),
              _DocumentsTab(driverId: widget.driverId),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildOverviewTab(Driver driver) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: _SectionCard(
              title: 'Contact Information',
              child: Column(
                children: [
                  _InfoRow(
                      icon: Icons.phone_outlined,
                      label: 'Phone',
                      value: driver.phone),
                  const SizedBox(height: 8),
                  _InfoRow(
                      icon: Icons.email_outlined,
                      label: 'Email',
                      value: driver.email),
                ],
              ),
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: _SectionCard(
              title: 'Employment',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _LabeledValue(
                    label: 'Employee ID',
                    value: driver.employeeId,
                  ),
                  const SizedBox(height: 12),
                  _LabeledValue(
                    label: 'Hire Date',
                    value: DateFormat('MMMM d, yyyy')
                        .format(driver.hireDate),
                  ),
                  const SizedBox(height: 12),
                  _LabeledValue(
                    label: 'Status',
                    valueWidget: Row(
                      children: [
                        Container(
                          width: 8,
                          height: 8,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: driver.isActive
                                ? AppColors.success
                                : AppColors.danger,
                          ),
                        ),
                        const SizedBox(width: 6),
                        Text(
                          driver.isActive ? 'Active' : 'Inactive',
                          style: const TextStyle(
                              fontSize: 14, color: Color(0xFF111827)),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ─── Sidebar ──────────────────────────────────────────────────────────────────

class _DriverSidebar extends StatelessWidget {
  final Driver driver;
  final bool isEditing;
  final TextEditingController firstNameCtrl;
  final TextEditingController lastNameCtrl;
  final TextEditingController phoneCtrl;
  final TextEditingController emailCtrl;
  final TextEditingController employeeIdCtrl;
  final DateTime hireDate;
  final bool isActive;
  final ValueChanged<DateTime> onHireDateChanged;
  final ValueChanged<bool> onActiveChanged;

  const _DriverSidebar({
    required this.driver,
    required this.isEditing,
    required this.firstNameCtrl,
    required this.lastNameCtrl,
    required this.phoneCtrl,
    required this.emailCtrl,
    required this.employeeIdCtrl,
    required this.hireDate,
    required this.isActive,
    required this.onHireDateChanged,
    required this.onActiveChanged,
  });

  @override
  Widget build(BuildContext context) {
    final initials =
        '${driver.firstName.isNotEmpty ? driver.firstName[0].toUpperCase() : ''}${driver.lastName.isNotEmpty ? driver.lastName[0].toUpperCase() : ''}';

    final (statusLabel, statusColor) = switch (driver.status) {
      DriverStatus.available => ('Available', AppColors.success),
      DriverStatus.onTrip =>
        ('On Trip', const Color(0xFFF59E0B)),
      DriverStatus.offDuty => ('Off Duty', AppColors.brandGray),
    };

    return Container(
      width: 260,
      color: Colors.white,
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Avatar
          Center(
            child: Container(
              width: 80,
              height: 80,
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(
                    color: AppColors.primary.withValues(alpha: 0.25)),
              ),
              child: Center(
                child: Text(
                  initials,
                  style: const TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.w700,
                      color: AppColors.primary),
                ),
              ),
            ),
          ),
          const SizedBox(height: 14),
          if (!isEditing) ...[
            Center(
              child: Text(
                driver.fullName,
                textAlign: TextAlign.center,
                style: const TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827)),
              ),
            ),
            const SizedBox(height: 4),
            Center(
              child: Text(
                driver.employeeId,
                textAlign: TextAlign.center,
                style: const TextStyle(
                    fontSize: 13, color: AppColors.brandGray),
              ),
            ),
          ] else ...[
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: firstNameCtrl,
                    decoration: _inputDec('First Name'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextField(
                    controller: lastNameCtrl,
                    decoration: _inputDec('Last Name'),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            TextField(
              controller: employeeIdCtrl,
              decoration: _inputDec('Employee ID'),
            ),
          ],
          const SizedBox(height: 12),
          // Status badge
          Center(
            child: Container(
              padding:
                  const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
              decoration: BoxDecoration(
                color: statusColor.withValues(alpha: 0.12),
                border: Border.all(
                    color: statusColor.withValues(alpha: 0.35)),
                borderRadius: BorderRadius.circular(20),
              ),
              child: Text(
                statusLabel,
                style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: statusColor,
                    letterSpacing: 0.5),
              ),
            ),
          ),
          const SizedBox(height: 20),
          const Divider(),
          const SizedBox(height: 12),
          if (isEditing) ...[
            _SidebarInfoRow(label: 'Phone'),
            const SizedBox(height: 4),
            TextField(
                controller: phoneCtrl, decoration: _inputDec('Phone')),
            const SizedBox(height: 14),
            _SidebarInfoRow(label: 'Email'),
            const SizedBox(height: 4),
            TextField(
                controller: emailCtrl, decoration: _inputDec('Email')),
            const SizedBox(height: 14),
            // Hire date
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
              borderRadius: BorderRadius.circular(10),
              child: InputDecorator(
                decoration: _inputDec('Hire Date').copyWith(
                    suffixIcon: const Icon(
                        Icons.calendar_today_outlined,
                        size: 16)),
                child: Text(
                  DateFormat('MMM d, yyyy').format(hireDate),
                  style: const TextStyle(
                      fontSize: 13, color: Color(0xFF111827)),
                ),
              ),
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                const Expanded(
                  child: Text('Active',
                      style: TextStyle(
                          fontSize: 13, color: AppColors.textPrimary)),
                ),
                Switch(
                  value: isActive,
                  onChanged: onActiveChanged,
                  activeColor: AppColors.primary,
                ),
              ],
            ),
          ] else ...[
            _SidebarInfoRow(
                label: 'Hire Date',
                value: DateFormat('MMM d, yyyy').format(driver.hireDate)),
            const SizedBox(height: 14),
            _SidebarInfoRow(
              label: 'Compliance',
              valueWidget: driver.hasExpiringDocuments
                  ? const Row(
                      children: [
                        Icon(Icons.warning_amber_rounded,
                            size: 14, color: AppColors.warning),
                        SizedBox(width: 4),
                        Flexible(
                          child: Text(
                            'Document expiring soon',
                            style: TextStyle(
                                fontSize: 12,
                                color: AppColors.warning,
                                fontWeight: FontWeight.w600),
                          ),
                        ),
                      ],
                    )
                  : const Text('All clear',
                      style: TextStyle(
                          fontSize: 13, color: AppColors.success)),
            ),
          ],
        ],
      ),
    );
  }

  InputDecoration _inputDec(String hint) => InputDecoration(
        hintText: hint,
        isDense: true,
        border:
            OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      );
}

class _SidebarInfoRow extends StatelessWidget {
  final String label;
  final String? value;
  final Widget? valueWidget;

  const _SidebarInfoRow({required this.label, this.value, this.valueWidget});

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
        if (valueWidget != null)
          valueWidget!
        else
          Text(
            value ?? '—',
            style:
                const TextStyle(fontSize: 14, color: Color(0xFF111827)),
          ),
      ],
    );
  }
}

// ─── Documents Tab ────────────────────────────────────────────────────────────

class _DocumentsTab extends ConsumerStatefulWidget {
  final String driverId;
  const _DocumentsTab({required this.driverId});

  @override
  ConsumerState<_DocumentsTab> createState() => _DocumentsTabState();
}

class _DocumentsTabState extends ConsumerState<_DocumentsTab> {
  Future<void> _showUploadDialog() async {
    DocumentType docType = DocumentType.drugAndAlcoholTest;
    DateTime? expiryDate;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Upload Document',
              style: TextStyle(fontWeight: FontWeight.w700)),
          content: SizedBox(
            width: 360,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                DropdownButtonFormField<DocumentType>(
                  value: docType,
                  decoration: InputDecoration(
                    labelText: 'Document Type',
                    border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(10)),
                    isDense: true,
                  ),
                  items: DocumentType.values
                      .map((t) => DropdownMenuItem(
                            value: t,
                            child: Text(_docTypeLabel(t)),
                          ))
                      .toList(),
                  onChanged: (v) {
                    if (v != null) setDialogState(() => docType = v);
                  },
                ),
                const SizedBox(height: 12),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('Expiry Date (optional)',
                      style: TextStyle(
                          fontSize: 13, color: AppColors.brandGray)),
                  subtitle: Text(
                    expiryDate != null
                        ? DateFormat('MMM d, yyyy').format(expiryDate!)
                        : 'Tap to set',
                    style: const TextStyle(
                        fontWeight: FontWeight.w600, fontSize: 14),
                  ),
                  trailing: const Icon(Icons.calendar_today_outlined,
                      size: 18),
                  onTap: () async {
                    final picked = await showDatePicker(
                      context: ctx,
                      initialDate: DateTime.now()
                          .add(const Duration(days: 365)),
                      firstDate: DateTime.now(),
                      lastDate: DateTime(2040),
                    );
                    if (picked != null) {
                      setDialogState(() => expiryDate = picked);
                    }
                  },
                ),
                const SizedBox(height: 8),
                const Text(
                  'File upload requires the device file picker — coming in next release.',
                  style: TextStyle(
                      fontSize: 11, color: AppColors.brandGray),
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, false),
              style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary),
              child: const Text('Upload'),
            ),
          ],
        ),
      ),
    );
    if (confirmed == true) {
      // File picker integration — placeholder until file_picker is wired
    }
  }

  String _docTypeLabel(DocumentType t) => switch (t) {
        DocumentType.drugAndAlcoholTest => 'Drug & Alcohol Test',
        DocumentType.driversLicense => "Driver's License",
        DocumentType.policeRecordCheck => 'Police Record Check',
        DocumentType.driverAbstract => 'Driver Abstract',
        DocumentType.norcatOrientation => 'NORCAT Orientation (Mine Site)',
        DocumentType.whmis => 'WHMIS (Optional)',
      };

  @override
  Widget build(BuildContext context) {
    final docsAsync =
        ref.watch(driverDocumentsProvider(widget.driverId));

    return Column(
      children: [
        // Header row
        Container(
          color: Colors.white,
          padding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          child: Row(
            children: [
              const Text(
                'Compliance Documents',
                style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827)),
              ),
              const Spacer(),
              TextButton.icon(
                onPressed: _showUploadDialog,
                icon: const Icon(Icons.upload_rounded, size: 16),
                label: const Text('Upload'),
                style: TextButton.styleFrom(
                    foregroundColor: AppColors.primary),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: docsAsync.when(
            loading: () =>
                const Center(child: CircularProgressIndicator()),
            error: (e, _) => Center(
              child: Text('Error: $e',
                  style:
                      const TextStyle(color: AppColors.brandGray)),
            ),
            data: (docs) {
              if (docs.isEmpty) {
                return Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.folder_open_outlined,
                          size: 48, color: AppColors.brandGray),
                      const SizedBox(height: 12),
                      const Text('No documents on file',
                          style: TextStyle(
                              color: AppColors.brandGray,
                              fontSize: 15)),
                      const SizedBox(height: 4),
                      const Text(
                          'Upload compliance documents for this driver.',
                          style: TextStyle(
                              fontSize: 12,
                              color: AppColors.brandGray)),
                      const SizedBox(height: 16),
                      OutlinedButton.icon(
                        onPressed: _showUploadDialog,
                        icon: const Icon(Icons.upload_rounded),
                        label: const Text('Upload Document'),
                      ),
                    ],
                  ),
                );
              }
              return ListView.separated(
                padding: const EdgeInsets.all(16),
                itemCount: docs.length,
                separatorBuilder: (_, __) =>
                    const SizedBox(height: 8),
                itemBuilder: (_, i) => _DocumentCard(
                  doc: docs[i],
                  onDelete: () async {
                    final messenger = ScaffoldMessenger.of(context);
                    final confirmed = await showDialog<bool>(
                      context: context,
                      builder: (ctx) => AlertDialog(
                        title: const Text('Delete Document'),
                        content: Text(
                            'Delete "${docs[i].documentTypeLabel}"? This cannot be undone.'),
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
                    if (confirmed == true && mounted) {
                      try {
                        await ref
                            .read(driverDocumentsProvider(
                                    widget.driverId)
                                .notifier)
                            .deleteDocument(docs[i].id);
                      } catch (e) {
                        messenger.showSnackBar(
                          SnackBar(
                              content: Text('Failed: $e'),
                              backgroundColor: AppColors.danger),
                        );
                      }
                    }
                  },
                  onDownload: () async {
                    final messenger = ScaffoldMessenger.of(context);
                    try {
                      final Uint8List bytes = await ref
                          .read(driverDocumentsProvider(
                                  widget.driverId)
                              .notifier)
                          .downloadDocument(docs[i].id);
                      messenger.showSnackBar(
                        SnackBar(
                            content: Text(
                                'Downloaded ${docs[i].fileName} (${bytes.length} bytes)')),
                      );
                    } catch (e) {
                      messenger.showSnackBar(
                        SnackBar(
                            content: Text('Download failed: $e'),
                            backgroundColor: AppColors.danger),
                      );
                    }
                  },
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}

class _DocumentCard extends StatelessWidget {
  final DriverDocument doc;
  final VoidCallback onDelete;
  final VoidCallback onDownload;

  const _DocumentCard({
    required this.doc,
    required this.onDelete,
    required this.onDownload,
  });

  @override
  Widget build(BuildContext context) {
    final (iconColor, bgColor) = doc.isExpiringSoon
        ? (AppColors.warning, const Color(0xFFFFF8EC))
        : (AppColors.primary, const Color(0xFFF0F4FF));

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(
          color: doc.isExpiringSoon
              ? const Color(0xFFFFE0A0)
              : const Color(0xFFE5E7EB),
        ),
        borderRadius: BorderRadius.circular(12),
      ),
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: bgColor,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(Icons.description_outlined,
                color: iconColor, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  doc.documentTypeLabel,
                  style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827)),
                ),
                const SizedBox(height: 2),
                Text(
                  doc.fileName,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.brandGray),
                  overflow: TextOverflow.ellipsis,
                ),
                if (doc.expiryDate != null) ...[
                  const SizedBox(height: 2),
                  Row(
                    children: [
                      if (doc.isExpiringSoon)
                        const Icon(Icons.warning_amber_rounded,
                            size: 12, color: AppColors.warning),
                      if (doc.isExpiringSoon) const SizedBox(width: 3),
                      Text(
                        'Expires ${DateFormat('MMM d, yyyy').format(doc.expiryDate!)}',
                        style: TextStyle(
                          fontSize: 11,
                          color: doc.isExpiringSoon
                              ? AppColors.warning
                              : AppColors.brandGray,
                          fontWeight: doc.isExpiringSoon
                              ? FontWeight.w600
                              : FontWeight.normal,
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          IconButton(
            icon: const Icon(Icons.download_rounded, size: 20),
            color: AppColors.primary,
            onPressed: onDownload,
            tooltip: 'Download',
          ),
          IconButton(
            icon: const Icon(Icons.delete_outline_rounded, size: 20),
            color: AppColors.danger,
            onPressed: onDelete,
            tooltip: 'Delete',
          ),
        ],
      ),
    );
  }
}

// ─── Shared UI helpers ────────────────────────────────────────────────────────

class _SectionCard extends StatelessWidget {
  final String title;
  final Widget child;

  const _SectionCard({required this.title, required this.child});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [
          BoxShadow(
              color: Color(0x06000000),
              blurRadius: 12,
              offset: Offset(0, 4)),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 10),
            child: Text(
              title,
              style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827)),
            ),
          ),
          const Divider(height: 1, color: Color(0xFFF3F4F6)),
          Padding(
              padding: const EdgeInsets.all(16), child: child),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _InfoRow(
      {required this.icon, required this.label, required this.value});

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
                  style: const TextStyle(
                      fontSize: 11, color: AppColors.brandGray)),
              Text(value,
                  style: const TextStyle(
                      fontSize: 13, color: Color(0xFF111827))),
            ],
          ),
        ),
      ],
    );
  }
}

class _LabeledValue extends StatelessWidget {
  final String label;
  final String? value;
  final Widget? valueWidget;

  const _LabeledValue({required this.label, this.value, this.valueWidget});

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
        const SizedBox(height: 2),
        valueWidget ??
            Text(
              value ?? '—',
              style: const TextStyle(
                  fontSize: 14, color: Color(0xFF111827)),
            ),
      ],
    );
  }
}
