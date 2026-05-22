import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import '../../../../core/theme/app_colors.dart';
import '../providers/client_detail_provider.dart';
import '../providers/clients_provider.dart';
import '../widgets/billing_info_panel.dart';
import '../widgets/client_profile_header.dart';
import '../widgets/compliance_notes_panel.dart';
import '../widgets/contract_summary_panel.dart';
import 'client_form_sheet.dart';

class ClientDetailPage extends ConsumerWidget {
  final String clientId;
  const ClientDetailPage({super.key, required this.clientId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final clientAsync = ref.watch(clientDetailProvider(clientId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        title: const Text(
          'Client Profile',
          style: TextStyle(fontSize: 17, fontWeight: FontWeight.w700, color: Color(0xFF111827)),
        ),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1),
          child: Container(height: 1, color: const Color(0xFFE5E7EB)),
        ),
      ),
      body: clientAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('Error: $e')),
        data: (client) => DefaultTabController(
          length: 3,
          child: NestedScrollView(
            headerSliverBuilder: (_, __) => [
              SliverToBoxAdapter(child: ClientProfileHeader(client: client)),
              SliverToBoxAdapter(
                child: Container(
                  color: Colors.white,
                  child: const TabBar(
                    labelColor: AppColors.primary,
                    unselectedLabelColor: AppColors.brandGray,
                    indicatorColor: AppColors.primary,
                    tabs: [
                      Tab(text: 'Overview'),
                      Tab(text: 'Trip History'),
                      Tab(text: 'Documents'),
                    ],
                  ),
                ),
              ),
            ],
            body: TabBarView(
              children: [
                // Overview tab
                ListView(
                  padding: const EdgeInsets.only(bottom: 100),
                  children: [
                    ContractSummaryPanel(
                      contract: client.activeContract,
                      isAdmin: true,
                    ),
                    BillingInfoPanel(client: client),
                    ComplianceNotesPanel(client: client),
                    const SizedBox(height: 12),
                    // Last synced indicator
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: Text(
                        'Last synced: ${DateFormat('MMM d, h:mm a').format(DateTime.now())}',
                        style: const TextStyle(fontSize: 11, color: AppColors.brandGray),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],
                ),
                // Trip History tab — placeholder
                const Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.route_outlined, size: 48, color: AppColors.brandGray),
                      SizedBox(height: 12),
                      Text('Trip history coming soon', style: TextStyle(color: AppColors.brandGray)),
                    ],
                  ),
                ),
                // Documents tab — placeholder
                const Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.folder_open_outlined, size: 48, color: AppColors.brandGray),
                      SizedBox(height: 12),
                      Text('Documents coming soon', style: TextStyle(color: AppColors.brandGray)),
                      SizedBox(height: 6),
                      Text('7-year retention per CRA requirements', style: TextStyle(fontSize: 11, color: AppColors.brandGray)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
      floatingActionButton: clientAsync.whenData((client) {
        return Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            FloatingActionButton.extended(
              heroTag: 'new_trip',
              onPressed: () {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Trip dispatch coming soon')),
                );
              },
              backgroundColor: AppColors.success,
              icon: const Icon(Icons.add_road_rounded),
              label: const Text('New Trip'),
            ),
            const SizedBox(height: 10),
            FloatingActionButton(
              heroTag: 'edit_client',
              onPressed: () async {
                final result = await showModalBottomSheet<bool>(
                  context: context,
                  isScrollControlled: true,
                  backgroundColor: Colors.transparent,
                  builder: (_) => ClientFormSheet(client: client),
                );
                if (result == true) {
                  ref.invalidate(clientDetailProvider(clientId));
                  ref.invalidate(clientsProvider);
                }
              },
              backgroundColor: AppColors.primary,
              child: const Icon(Icons.edit_rounded, color: Colors.white),
            ),
          ],
        );
      }).valueOrNull ?? const SizedBox.shrink(),
    );
  }
}
