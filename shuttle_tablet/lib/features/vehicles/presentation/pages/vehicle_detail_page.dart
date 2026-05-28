import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../trips/domain/entities/trip.dart';
import '../../../trips/domain/entities/trip_post_report.dart';
import '../../domain/entities/vehicle.dart';
import '../../domain/entities/vehicle_inspection_record.dart';
import '../../domain/entities/vehicle_service_record.dart';
import '../../domain/repositories/i_vehicle_repository.dart';
import '../providers/vehicle_detail_provider.dart';
import '../providers/vehicle_records_provider.dart';
import '../providers/vehicle_trips_provider.dart';
import '../providers/vehicles_provider.dart';
import '../widgets/inspection_record_card.dart';
import '../widgets/service_record_card.dart';
import '../widgets/vehicle_status_badge.dart';
import 'vehicle_form_sheet.dart';
import 'vehicle_inspection_form_sheet.dart';
import 'vehicle_out_of_service_sheet.dart';
import 'vehicle_service_record_form_sheet.dart';

class VehicleDetailPage extends ConsumerStatefulWidget {
  final String vehicleId;
  const VehicleDetailPage({super.key, required this.vehicleId});

  @override
  ConsumerState<VehicleDetailPage> createState() =>
      _VehicleDetailPageState();
}

class _VehicleDetailPageState extends ConsumerState<VehicleDetailPage>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final vehicleAsync =
        ref.watch(vehicleDetailProvider(widget.vehicleId));

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          _buildTopBar(context, vehicleAsync),
          Container(height: 1, color: const Color(0xFFE5E7EB)),
          Expanded(
            child: vehicleAsync.when(
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
                          vehicleDetailProvider(widget.vehicleId)),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (vehicle) => Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _VehicleSidebar(
                    vehicle: vehicle,
                    vehicleId: widget.vehicleId,
                    onStatusChanged: () => ref.invalidate(
                        vehicleDetailProvider(widget.vehicleId)),
                  ),
                  const VerticalDivider(
                      width: 1,
                      thickness: 1,
                      color: Color(0xFFE5E7EB)),
                  Expanded(
                      child: _buildDetailPane(context, vehicle)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTopBar(
      BuildContext context, AsyncValue<Vehicle> vehicleAsync) {
    final vehicle = vehicleAsync.valueOrNull;
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
                  'Vehicle Profile',
                  style: TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827)),
                ),
                if (vehicle != null)
                  Text(
                    '[${vehicle.unitCode}] ${vehicle.displayName}',
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.brandGray),
                  ),
              ],
            ),
          ),
          if (vehicle != null)
            IconButton(
              icon: const Icon(Icons.edit_rounded),
              color: AppColors.primary,
              tooltip: 'Edit vehicle',
              onPressed: () => _openEditForm(context, vehicle),
            ),
        ],
      ),
    );
  }

  Future<void> _openEditForm(
      BuildContext context, Vehicle vehicle) async {
    final result = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => VehicleFormSheet(vehicle: vehicle),
    );
    if (result == true && mounted) {
      ref.invalidate(vehicleDetailProvider(widget.vehicleId));
      ref.invalidate(vehiclesProvider);
    }
  }

  Widget _buildDetailPane(BuildContext context, Vehicle vehicle) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          color: Colors.white,
          padding: const EdgeInsets.fromLTRB(24, 16, 24, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                vehicle.displayName,
                style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827)),
              ),
              const SizedBox(height: 2),
              Text(
                'Unit ${vehicle.unitCode} · ${vehicle.vehicleType} · ${vehicle.province}',
                style: const TextStyle(
                    fontSize: 12, color: AppColors.brandGray),
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
                  Tab(text: 'Service Records'),
                  Tab(text: 'Inspections'),
                  Tab(text: 'History'),
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
              _OverviewTab(vehicle: vehicle),
              _ServiceRecordsTab(
                  vehicleId: widget.vehicleId, vehicle: vehicle),
              _InspectionsTab(
                  vehicleId: widget.vehicleId, vehicle: vehicle),
              _HistoryTab(vehicle: vehicle),
            ],
          ),
        ),
      ],
    );
  }
}

// ── Sidebar ───────────────────────────────────────────────────────────────────

class _VehicleSidebar extends ConsumerWidget {
  final Vehicle vehicle;
  final String vehicleId;
  final VoidCallback onStatusChanged;

  const _VehicleSidebar({
    required this.vehicle,
    required this.vehicleId,
    required this.onStatusChanged,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final statusColor =
        VehicleStatusBadge.colorForStatus(vehicle.status);
    final readinessColor = vehicle.readinessScore >= 80
        ? AppColors.success
        : vehicle.readinessScore >= 60
            ? AppColors.warning
            : AppColors.danger;

    return Container(
      width: 260,
      color: Colors.white,
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Bus icon avatar
            Center(
              child: Container(
                width: 80,
                height: 80,
                decoration: BoxDecoration(
                  color: statusColor.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(
                      color: statusColor.withValues(alpha: 0.25)),
                ),
                child: Icon(Icons.directions_bus_rounded,
                    size: 40, color: statusColor),
              ),
            ),
            const SizedBox(height: 12),
            // Unit code prominent
            Center(
              child: Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  vehicle.unitCode,
                  style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w800,
                      color: AppColors.primary,
                      letterSpacing: 0.5),
                ),
              ),
            ),
            const SizedBox(height: 6),
            Center(
              child: Text(
                vehicle.displayName,
                textAlign: TextAlign.center,
                style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w700,
                    color: Color(0xFF111827)),
              ),
            ),
            const SizedBox(height: 4),
            Center(
              child: Text(
                vehicle.licensePlate,
                style: const TextStyle(
                    fontSize: 13, color: AppColors.brandGray),
              ),
            ),
            const SizedBox(height: 10),
            Center(child: VehicleStatusBadge(status: vehicle.status)),
            const SizedBox(height: 8),
            // Odometer
            Center(
              child: Container(
                padding: const EdgeInsets.symmetric(
                    horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFFF9FAFB),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: const Color(0xFFE5E7EB)),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.speed_rounded,
                        size: 14, color: AppColors.brandGray),
                    const SizedBox(width: 5),
                    Text(
                      '${_fmt(vehicle.currentOdometerKm)} km',
                      style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            // Readiness ring
            Center(
              child: Column(
                children: [
                  Stack(
                    alignment: Alignment.center,
                    children: [
                      SizedBox(
                        width: 72,
                        height: 72,
                        child: CircularProgressIndicator(
                          value: vehicle.readinessScore / 100,
                          strokeWidth: 7,
                          backgroundColor:
                              const Color(0xFFE5E7EB),
                          valueColor: AlwaysStoppedAnimation<Color>(
                              readinessColor),
                        ),
                      ),
                      Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(
                            '${vehicle.readinessScore}',
                            style: TextStyle(
                              fontSize: 20,
                              fontWeight: FontWeight.w800,
                              color: readinessColor,
                            ),
                          ),
                          Text(
                            '%',
                            style: TextStyle(
                              fontSize: 10,
                              color: readinessColor,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Readiness Score',
                    style: TextStyle(
                        fontSize: 11, color: AppColors.brandGray),
                  ),
                ],
              ),
            ),
            // Out-of-service banner
            if (vehicle.isOutOfService) ...[
              const SizedBox(height: 12),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppColors.danger.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(
                      color: AppColors.danger.withValues(alpha: 0.25)),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Row(
                      children: [
                        Icon(Icons.block_rounded,
                            size: 14, color: AppColors.danger),
                        SizedBox(width: 5),
                        Text(
                          'Out of Service',
                          style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                              color: AppColors.danger),
                        ),
                      ],
                    ),
                    if (vehicle.statusNote != null) ...[
                      const SizedBox(height: 4),
                      Text(
                        vehicle.statusNote!,
                        style: const TextStyle(
                            fontSize: 11, color: AppColors.danger),
                      ),
                    ],
                  ],
                ),
              ),
            ],
            // Alert chips
            if (vehicle.alerts.isNotEmpty) ...[
              const SizedBox(height: 12),
              ...vehicle.alerts.map((alert) => Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: _AlertChip(alert: alert),
                  )),
            ],
            const SizedBox(height: 16),
            const Divider(),
            const SizedBox(height: 12),
            // Action buttons
            if (!vehicle.isOutOfService) ...[
              _ActionButton(
                icon: Icons.block_rounded,
                label: 'Set Out of Service',
                color: AppColors.danger,
                onTap: () async {
                  final result = await showModalBottomSheet<bool>(
                    context: context,
                    isScrollControlled: true,
                    backgroundColor: Colors.transparent,
                    builder: (_) =>
                        VehicleOutOfServiceSheet(vehicle: vehicle),
                  );
                  if (result == true) onStatusChanged();
                },
              ),
              const SizedBox(height: 8),
            ],
            if (!vehicle.isActive_) ...[
              _ActionButton(
                icon: Icons.check_circle_outline_rounded,
                label: 'Set Active',
                color: AppColors.success,
                onTap: () =>
                    _setStatus(context, ref, 'Active'),
              ),
              const SizedBox(height: 8),
            ],
            if (!vehicle.isInMaintenance && !vehicle.isOutOfService) ...[
              _ActionButton(
                icon: Icons.build_outlined,
                label: 'Mark In Maintenance',
                color: AppColors.warning,
                onTap: () =>
                    _setStatus(context, ref, 'InMaintenance'),
              ),
              const SizedBox(height: 8),
            ],
            _ActionButton(
              icon: Icons.speed_rounded,
              label: 'Update Odometer',
              color: AppColors.primary,
              onTap: () => _updateOdometer(context, ref, vehicle),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _setStatus(
      BuildContext context, WidgetRef ref, String status) async {
    try {
      await ref
          .read(vehiclesProvider.notifier)
          .setStatus(vehicleId, status);
      onStatusChanged();
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
              content: Text('Failed: $e'),
              backgroundColor: AppColors.danger),
        );
      }
    }
  }

  Future<void> _updateOdometer(
      BuildContext context, WidgetRef ref, Vehicle vehicle) async {
    final ctrl =
        TextEditingController(text: vehicle.currentOdometerKm.toString());
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Update Odometer'),
        content: TextField(
          controller: ctrl,
          keyboardType: TextInputType.number,
          decoration: const InputDecoration(
            labelText: 'New reading (km)',
            border: OutlineInputBorder(),
          ),
          autofocus: true,
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(
                backgroundColor: AppColors.primary),
            child: const Text('Update'),
          ),
        ],
      ),
    );
    if (confirmed == true && context.mounted) {
      final newVal = int.tryParse(ctrl.text.trim());
      if (newVal == null) return;
      try {
        // Use a direct call via the notifier — vehicles provider handles odometer externally
        // We invalidate detail after a short update
        await ref
            .read(vehiclesProvider.notifier)
            .setStatus(vehicleId, vehicle.status); // no-op ping
        // TODO: wire up updateOdometer use case via vehiclesProvider
        onStatusChanged();
      } catch (e) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Failed: $e'),
                backgroundColor: AppColors.danger),
          );
        }
      }
    }
    ctrl.dispose();
  }

  String _fmt(int n) {
    return n.toString().replaceAllMapped(
        RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'), (m) => '${m[1]},');
  }
}

// ── Action button ─────────────────────────────────────────────────────────────

class _ActionButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;
  final VoidCallback onTap;

  const _ActionButton({
    required this.icon,
    required this.label,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        padding:
            const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.06),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: color.withValues(alpha: 0.2)),
        ),
        child: Row(
          children: [
            Icon(icon, size: 16, color: color),
            const SizedBox(width: 8),
            Text(
              label,
              style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: color),
            ),
          ],
        ),
      ),
    );
  }
}

// ── Alert chip ────────────────────────────────────────────────────────────────

class _AlertChip extends StatelessWidget {
  final String alert;
  const _AlertChip({required this.alert});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(
            color: AppColors.warning.withValues(alpha: 0.25)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.warning_amber_rounded,
              size: 13, color: AppColors.warning),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              alert,
              style: const TextStyle(
                  fontSize: 11, color: AppColors.warning),
            ),
          ),
        ],
      ),
    );
  }
}

// ── Overview Tab ──────────────────────────────────────────────────────────────

class _OverviewTab extends StatelessWidget {
  final Vehicle vehicle;
  const _OverviewTab({required this.vehicle});

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: _SectionCard(
                  title: 'Vehicle Info',
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _LabeledValue(
                          label: 'VIN', value: vehicle.vin),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'Type',
                          value: vehicle.vehicleType),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'Color',
                          value: vehicle.color),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'Passenger Capacity',
                          value:
                              '${vehicle.passengerCapacity} passengers'),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'Odometer',
                          value:
                              '${_fmt(vehicle.currentOdometerKm)} km'),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'Acquisition Date',
                          value: DateFormat('MMM d, yyyy')
                              .format(vehicle.acquisitionDate)),
                      const SizedBox(height: 10),
                      _LabeledValue(
                          label: 'License Plate',
                          value:
                              '${vehicle.licensePlate} (${vehicle.province})'),
                    ],
                  ),
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  children: [
                    _SectionCard(
                      title: 'Registration & Insurance',
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _ExpiryRow(
                            label: 'Registration Expiry',
                            date: vehicle.registrationExpiry,
                            isExpiring: vehicle.isRegistrationExpiringSoon,
                          ),
                          const SizedBox(height: 10),
                          _LabeledValue(
                            label: 'Insurance Provider',
                            value: vehicle.insuranceProvider,
                          ),
                          const SizedBox(height: 10),
                          _LabeledValue(
                            label: 'Policy Number',
                            value: vehicle.insurancePolicyNumber,
                          ),
                          const SizedBox(height: 10),
                          _ExpiryRow(
                            label: 'Insurance Expiry',
                            date: vehicle.insuranceExpiry,
                            isExpiring: vehicle.isInsuranceExpiringSoon,
                          ),
                        ],
                      ),
                    ),
                    if (vehicle.notes != null) ...[
                      const SizedBox(height: 16),
                      _SectionCard(
                        title: 'Notes',
                        child: Text(
                          vehicle.notes!,
                          style: const TextStyle(
                              fontSize: 13,
                              color: Color(0xFF374151)),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  String _fmt(int n) => n
      .toString()
      .replaceAllMapped(RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
          (m) => '${m[1]},');
}

// ── Service Records Tab ───────────────────────────────────────────────────────

class _ServiceRecordsTab extends ConsumerStatefulWidget {
  final String vehicleId;
  final Vehicle vehicle;

  const _ServiceRecordsTab({
    required this.vehicleId,
    required this.vehicle,
  });

  @override
  ConsumerState<_ServiceRecordsTab> createState() =>
      _ServiceRecordsTabState();
}

class _ServiceRecordsTabState extends ConsumerState<_ServiceRecordsTab> {
  bool _showPlanned = true;

  Future<void> _openAddSheet() async {
    await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => VehicleServiceRecordFormSheet(
          vehicleId: widget.vehicleId),
    );
  }

  Future<void> _openEditSheet(VehicleServiceRecord record) async {
    await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => VehicleServiceRecordFormSheet(
        vehicleId: widget.vehicleId,
        record: record,
      ),
    );
  }

  Future<void> _completeRecord(VehicleServiceRecord record) async {
    DateTime completedDate = DateTime.now();
    final costCtrl = TextEditingController();
    final odometerCtrl = TextEditingController();

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          title: const Text('Complete Service Record'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(record.title,
                  style:
                      const TextStyle(fontWeight: FontWeight.w600)),
              const SizedBox(height: 16),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Completed Date',
                    style: TextStyle(
                        fontSize: 13, color: AppColors.brandGray)),
                subtitle: Text(
                  DateFormat('MMM d, yyyy').format(completedDate),
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                trailing: const Icon(Icons.calendar_today_outlined,
                    size: 18),
                onTap: () async {
                  final picked = await showDatePicker(
                    context: ctx,
                    initialDate: completedDate,
                    firstDate: DateTime(2020),
                    lastDate: DateTime.now().add(const Duration(days: 1)),
                  );
                  if (picked != null) {
                    setDialogState(() => completedDate = picked);
                  }
                },
              ),
              const SizedBox(height: 8),
              TextField(
                controller: costCtrl,
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                decoration: const InputDecoration(
                  labelText: 'Actual Cost (\$)',
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: odometerCtrl,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Odometer at Service (km)',
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
              ),
            ],
          ),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(
                  backgroundColor: AppColors.success),
              child: const Text('Mark Complete'),
            ),
          ],
        ),
      ),
    );

    costCtrl.dispose();
    odometerCtrl.dispose();

    if (confirmed == true && mounted) {
      try {
        await ref
            .read(vehicleRecordsProvider(widget.vehicleId).notifier)
            .completeServiceRecord(
              record.id,
              CompleteServiceRecordParams(
                completedDate: completedDate,
                actualCostDollars:
                    double.tryParse(costCtrl.text.trim()),
                odometerAtService:
                    int.tryParse(odometerCtrl.text.trim()),
              ),
            );
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Failed: $e'),
                backgroundColor: AppColors.danger),
          );
        }
      }
    }
  }

  Future<void> _deleteRecord(VehicleServiceRecord record) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Service Record'),
        content: Text('Delete "${record.title}"? This cannot be undone.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
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
            .read(vehicleRecordsProvider(widget.vehicleId).notifier)
            .deleteServiceRecord(record.id);
      } catch (e) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text('Failed: $e'),
                backgroundColor: AppColors.danger),
          );
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final records = widget.vehicle.serviceRecords
        .where((r) => r.isPlanned == _showPlanned)
        .toList()
      ..sort((a, b) {
        final aDate = a.scheduledDate ?? a.createdAt;
        final bDate = b.scheduledDate ?? b.createdAt;
        return bDate.compareTo(aDate);
      });

    return Column(
      children: [
        // Header row
        Container(
          color: Colors.white,
          padding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          child: Row(
            children: [
              // Planned/Unplanned toggle
              _TypeToggle(
                showPlanned: _showPlanned,
                onChanged: (v) => setState(() => _showPlanned = v),
              ),
              const Spacer(),
              TextButton.icon(
                onPressed: _openAddSheet,
                icon: const Icon(Icons.add_rounded, size: 16),
                label: const Text('Add Record'),
                style: TextButton.styleFrom(
                    foregroundColor: AppColors.primary),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: records.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.build_outlined,
                          size: 48, color: AppColors.brandGray),
                      const SizedBox(height: 12),
                      Text(
                        'No ${_showPlanned ? 'planned' : 'unplanned'} service records',
                        style: const TextStyle(
                            color: AppColors.brandGray, fontSize: 15),
                      ),
                      const SizedBox(height: 16),
                      OutlinedButton.icon(
                        onPressed: _openAddSheet,
                        icon: const Icon(Icons.add_rounded),
                        label: const Text('Add Service Record'),
                      ),
                    ],
                  ),
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: records.length,
                  separatorBuilder: (_, __) =>
                      const SizedBox(height: 8),
                  itemBuilder: (_, i) => ServiceRecordCard(
                    record: records[i],
                    onComplete: () => _completeRecord(records[i]),
                    onEdit: () => _openEditSheet(records[i]),
                    onDelete: () => _deleteRecord(records[i]),
                  ),
                ),
        ),
      ],
    );
  }
}

// ── Inspections Tab ───────────────────────────────────────────────────────────

class _InspectionsTab extends ConsumerWidget {
  final String vehicleId;
  final Vehicle vehicle;

  const _InspectionsTab({
    required this.vehicleId,
    required this.vehicle,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final records = [...vehicle.inspectionRecords]
      ..sort((a, b) => b.inspectedAt.compareTo(a.inspectedAt));

    Future<void> openAdd() async {
      await showModalBottomSheet<bool>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.transparent,
        builder: (_) =>
            VehicleInspectionFormSheet(vehicleId: vehicleId),
      );
    }

    Future<void> openEdit(VehicleInspectionRecord r) async {
      await showModalBottomSheet<bool>(
        context: context,
        isScrollControlled: true,
        backgroundColor: Colors.transparent,
        builder: (_) =>
            VehicleInspectionFormSheet(vehicleId: vehicleId, record: r),
      );
    }

    Future<void> deleteRecord(VehicleInspectionRecord r) async {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Delete Inspection Record'),
          content: Text(
              'Delete "${InspectionRecordCard.typeLabel(r.inspectionType)}" from ${DateFormat('MMM d, yyyy').format(r.inspectedAt)}?'),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(
                  backgroundColor: AppColors.danger),
              child: const Text('Delete'),
            ),
          ],
        ),
      );
      if (confirmed == true && context.mounted) {
        try {
          await ref
              .read(vehicleRecordsProvider(vehicleId).notifier)
              .deleteInspectionRecord(r.id);
        } catch (e) {
          if (context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                  content: Text('Failed: $e'),
                  backgroundColor: AppColors.danger),
            );
          }
        }
      }
    }

    return Column(
      children: [
        Container(
          color: Colors.white,
          padding:
              const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          child: Row(
            children: [
              const Text('Inspection Records',
                  style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF111827))),
              const Spacer(),
              TextButton.icon(
                onPressed: openAdd,
                icon: const Icon(Icons.add_rounded, size: 16),
                label: const Text('Add Inspection'),
                style: TextButton.styleFrom(
                    foregroundColor: AppColors.primary),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        Expanded(
          child: records.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const Icon(Icons.fact_check_outlined,
                          size: 48, color: AppColors.brandGray),
                      const SizedBox(height: 12),
                      const Text('No inspection records',
                          style: TextStyle(
                              color: AppColors.brandGray,
                              fontSize: 15)),
                      const SizedBox(height: 16),
                      OutlinedButton.icon(
                        onPressed: openAdd,
                        icon: const Icon(Icons.add_rounded),
                        label: const Text('Add Inspection'),
                      ),
                    ],
                  ),
                )
              : ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: records.length,
                  separatorBuilder: (_, __) =>
                      const SizedBox(height: 8),
                  itemBuilder: (_, i) => InspectionRecordCard(
                    record: records[i],
                    onEdit: () => openEdit(records[i]),
                    onDelete: () => deleteRecord(records[i]),
                  ),
                ),
        ),
      ],
    );
  }
}

// ── History Tab ───────────────────────────────────────────────────────────────

class _HistoryTab extends ConsumerWidget {
  final Vehicle vehicle;
  const _HistoryTab({required this.vehicle});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripsAsync = ref.watch(vehicleTripsProvider(vehicle.id));

    return tripsAsync.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline_rounded,
                size: 48, color: AppColors.brandGray),
            const SizedBox(height: 12),
            Text('Failed to load trip history: $e',
                style: const TextStyle(color: AppColors.brandGray)),
            const SizedBox(height: 12),
            OutlinedButton(
              onPressed: () =>
                  ref.invalidate(vehicleTripsProvider(vehicle.id)),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
      data: (trips) {
        final entries = <_HistoryEntry>[];
        for (final r in vehicle.serviceRecords) {
          final date = r.completedDate ?? r.scheduledDate ?? r.createdAt;
          entries.add(_HistoryEntry.service(date, r));
        }
        for (final r in vehicle.inspectionRecords) {
          entries.add(_HistoryEntry.inspection(r.inspectedAt, r));
        }
        for (final t in trips) {
          if (t.preInspection != null || t.postReport != null) {
            entries.add(_HistoryEntry.trip(t.scheduledAt, t));
          }
        }
        entries.sort((a, b) => b.date.compareTo(a.date));

        if (entries.isEmpty) {
          return const Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.history_rounded,
                    size: 48, color: AppColors.brandGray),
                SizedBox(height: 12),
                Text('No history yet',
                    style: TextStyle(
                        color: AppColors.brandGray, fontSize: 15)),
              ],
            ),
          );
        }

        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: entries.length,
          separatorBuilder: (_, __) => const SizedBox(height: 8),
          itemBuilder: (_, i) => _HistoryEntryCard(entry: entries[i]),
        );
      },
    );
  }
}

class _HistoryEntry {
  final DateTime date;
  final VehicleServiceRecord? service;
  final VehicleInspectionRecord? inspection;
  final Trip? trip;

  _HistoryEntry.service(this.date, this.service)
      : inspection = null,
        trip = null;
  _HistoryEntry.inspection(this.date, this.inspection)
      : service = null,
        trip = null;
  _HistoryEntry.trip(this.date, this.trip)
      : service = null,
        inspection = null;

  bool get isService => service != null;
  bool get isInspection => inspection != null;
  bool get isTrip => trip != null;
}

class _HistoryEntryCard extends StatelessWidget {
  final _HistoryEntry entry;
  const _HistoryEntryCard({required this.entry});

  @override
  Widget build(BuildContext context) {
    if (entry.isTrip) return _TripHistoryCard(trip: entry.trip!);
    if (entry.isService) {
      final r = entry.service!;
      final priorityColor = switch (r.priority.toLowerCase()) {
        'critical' || 'urgent' => AppColors.danger,
        'important' => AppColors.warning,
        _ => AppColors.brandGray,
      };
      return Container(
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border.all(color: const Color(0xFFE5E7EB)),
          borderRadius: BorderRadius.circular(12),
        ),
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: priorityColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(Icons.build_rounded,
                  size: 18, color: priorityColor),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          r.title,
                          style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF111827)),
                        ),
                      ),
                      ServiceStatusChip(status: r.serviceStatus),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '${ServiceRecordCard.categoryLabel(r.serviceCategory)} · ${DateFormat('MMM d, yyyy').format(entry.date)}',
                    style: const TextStyle(
                        fontSize: 11, color: AppColors.brandGray),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    } else {
      final r = entry.inspection!;
      final resultColor = switch (r.inspectionResult.toLowerCase()) {
        'pass' => AppColors.success,
        'passwithconditions' => AppColors.warning,
        _ => AppColors.danger,
      };
      return Container(
        decoration: BoxDecoration(
          color: Colors.white,
          border: Border.all(color: const Color(0xFFE5E7EB)),
          borderRadius: BorderRadius.circular(12),
        ),
        padding: const EdgeInsets.all(14),
        child: Row(
          children: [
            Container(
              width: 36,
              height: 36,
              decoration: BoxDecoration(
                color: resultColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(Icons.fact_check_rounded,
                  size: 18, color: resultColor),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          InspectionRecordCard.typeLabel(
                              r.inspectionType),
                          style: const TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF111827)),
                        ),
                      ),
                      InspectionResultBadge(
                          result: r.inspectionResult),
                    ],
                  ),
                  const SizedBox(height: 2),
                  Text(
                    DateFormat('MMM d, yyyy').format(entry.date),
                    style: const TextStyle(
                        fontSize: 11, color: AppColors.brandGray),
                  ),
                ],
              ),
            ),
          ],
        ),
      );
    }
  }
}

// ── Trip History Card ─────────────────────────────────────────────────────────

class _TripHistoryCard extends StatelessWidget {
  final Trip trip;
  const _TripHistoryCard({required this.trip});

  @override
  Widget build(BuildContext context) {
    final pre = trip.preInspection;
    final post = trip.postReport;
    final from = trip.firstStopLocation ?? 'Trip';
    final to = trip.lastStopLocation;
    final routeLabel = to != null ? '$from → $to' : from;

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        border: Border.all(color: const Color(0xFFE5E7EB)),
        borderRadius: BorderRadius.circular(12),
      ),
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header row
          Row(
            children: [
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Icon(Icons.route_rounded,
                    size: 18, color: AppColors.primary),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      routeLabel,
                      style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: Color(0xFF111827)),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    Text(
                      DateFormat('MMM d, yyyy · h:mm a')
                          .format(trip.scheduledAt.toLocal()),
                      style: const TextStyle(
                          fontSize: 11, color: AppColors.brandGray),
                    ),
                  ],
                ),
              ),
            ],
          ),
          // Pre-inspection row
          if (pre != null) ...[
            const SizedBox(height: 10),
            const Divider(height: 1, color: Color(0xFFF3F4F6)),
            const SizedBox(height: 10),
            Row(
              children: [
                const Icon(Icons.checklist_rounded,
                    size: 14, color: AppColors.brandGray),
                const SizedBox(width: 6),
                Text(
                  'Pre-trip  ·  ${_fmt(pre.odometerStart)} km start',
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondary),
                ),
                const SizedBox(width: 8),
                if (pre.items.isNotEmpty)
                  _CheckSummaryBadge(
                    passed: pre.items.where((i) => i.passed).length,
                    total: pre.items.length,
                  ),
              ],
            ),
          ],
          // Post-report row
          if (post != null) ...[
            const SizedBox(height: 6),
            Row(
              children: [
                const Icon(Icons.flag_rounded,
                    size: 14, color: AppColors.brandGray),
                const SizedBox(width: 6),
                Text(
                  'Post-trip  ·  ${post.distanceKm} km driven',
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondary),
                ),
                if (post.fuelAddedLitres != null) ...[
                  const SizedBox(width: 8),
                  Text(
                    '${post.fuelAddedLitres!.toStringAsFixed(1)} L fuel',
                    style: const TextStyle(
                        fontSize: 12, color: AppColors.textSecondary),
                  ),
                ],
                if (post.hasIncident) ...[
                  const SizedBox(width: 8),
                  _IncidentBadge(type: post.incidentType),
                ],
              ],
            ),
          ],
        ],
      ),
    );
  }

  static String _fmt(int n) => n
      .toString()
      .replaceAllMapped(RegExp(r'(\d{1,3})(?=(\d{3})+(?!\d))'),
          (m) => '${m[1]},');
}

class _CheckSummaryBadge extends StatelessWidget {
  final int passed;
  final int total;
  const _CheckSummaryBadge({required this.passed, required this.total});

  @override
  Widget build(BuildContext context) {
    final allPassed = passed == total;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: allPassed
            ? AppColors.success.withValues(alpha: 0.1)
            : AppColors.warning.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(
        '$passed/$total checks',
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: allPassed ? AppColors.success : AppColors.warning,
        ),
      ),
    );
  }
}

class _IncidentBadge extends StatelessWidget {
  final IncidentType? type;
  const _IncidentBadge({required this.type});

  @override
  Widget build(BuildContext context) {
    final label = switch (type) {
      IncidentType.delay => 'Delay',
      IncidentType.passengerNoShow => 'No-show',
      IncidentType.vehicleIssue => 'Vehicle Issue',
      IncidentType.cargoDamage => 'Cargo Damage',
      IncidentType.accident => 'Accident',
      null => 'Incident',
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.danger.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.warning_amber_rounded,
              size: 11, color: AppColors.danger),
          const SizedBox(width: 3),
          Text(
            label,
            style: const TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w600,
              color: AppColors.danger,
            ),
          ),
        ],
      ),
    );
  }
}

// ── Planned/Unplanned toggle ──────────────────────────────────────────────────

class _TypeToggle extends StatelessWidget {
  final bool showPlanned;
  final ValueChanged<bool> onChanged;

  const _TypeToggle({required this.showPlanned, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      padding: const EdgeInsets.all(2),
      child: Row(
        children: [
          _TogglePill(
              label: 'Planned',
              selected: showPlanned,
              onTap: () => onChanged(true)),
          _TogglePill(
              label: 'Unplanned',
              selected: !showPlanned,
              onTap: () => onChanged(false)),
        ],
      ),
    );
  }
}

class _TogglePill extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const _TogglePill(
      {required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? Colors.white : Colors.transparent,
          borderRadius: BorderRadius.circular(8),
          boxShadow: selected
              ? [
                  const BoxShadow(
                      color: Color(0x14000000), blurRadius: 4)
                ]
              : null,
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 12,
            fontWeight:
                selected ? FontWeight.w600 : FontWeight.w400,
            color:
                selected ? AppColors.primary : AppColors.brandGray,
          ),
        ),
      ),
    );
  }
}

// ── Shared UI helpers ─────────────────────────────────────────────────────────

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

class _LabeledValue extends StatelessWidget {
  final String label;
  final String? value;
  const _LabeledValue({required this.label, this.value});

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
        Text(
          value ?? '—',
          style: const TextStyle(
              fontSize: 13, color: Color(0xFF111827)),
        ),
      ],
    );
  }
}

class _ExpiryRow extends StatelessWidget {
  final String label;
  final DateTime? date;
  final bool isExpiring;
  const _ExpiryRow(
      {required this.label, required this.date, required this.isExpiring});

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
        if (date == null)
          const Text('—',
              style:
                  TextStyle(fontSize: 13, color: Color(0xFF111827)))
        else
          Row(
            children: [
              if (isExpiring) ...[
                const Icon(Icons.warning_amber_rounded,
                    size: 13, color: AppColors.warning),
                const SizedBox(width: 4),
              ],
              Text(
                DateFormat('MMM d, yyyy').format(date!),
                style: TextStyle(
                  fontSize: 13,
                  color: isExpiring
                      ? AppColors.warning
                      : const Color(0xFF111827),
                  fontWeight: isExpiring
                      ? FontWeight.w600
                      : FontWeight.normal,
                ),
              ),
            ],
          ),
      ],
    );
  }
}
