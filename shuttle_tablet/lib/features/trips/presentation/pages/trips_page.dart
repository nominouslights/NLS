import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../providers/trips_provider.dart';
import '../widgets/trip_card.dart';
import '../widgets/trip_detail_workspace.dart';
import 'trip_manifest_form_page.dart';

class TripsPage extends ConsumerStatefulWidget {
  final TripServiceType? serviceType;
  const TripsPage({super.key, this.serviceType});

  @override
  ConsumerState<TripsPage> createState() => _TripsPageState();
}

class _TripsPageState extends ConsumerState<TripsPage> {
  String _search = '';
  TripStatus? _statusFilter;
  String? _selectedTripId;

  @override
  void initState() {
    super.initState();
    if (widget.serviceType != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        ref.read(tripsProvider.notifier).setFilter(
              serviceType: widget.serviceType,
            );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final tripsAsync = ref.watch(tripsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Row(
        children: [
          // ── Left Panel ───────────────────────────────────────────────────
          SizedBox(
            width: 380,
            child: Column(
              children: [
                // Search + filter header
                Container(
                  color: Colors.white,
                  padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
                  child: Column(
                    children: [
                      TextField(
                        decoration: InputDecoration(
                          hintText: 'Search trips…',
                          prefixIcon: const Icon(Icons.search_rounded, size: 20),
                          isDense: true,
                          filled: true,
                          fillColor: const Color(0xFFF9FAFB),
                          border: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
                          ),
                          enabledBorder: OutlineInputBorder(
                            borderRadius: BorderRadius.circular(12),
                            borderSide: const BorderSide(color: Color(0xFFE5E7EB)),
                          ),
                          contentPadding:
                              const EdgeInsets.symmetric(vertical: 10),
                        ),
                        onChanged: (v) => setState(() => _search = v),
                      ),
                      const SizedBox(height: 8),
                      _StatusFilterBar(
                        selected: _statusFilter,
                        onChanged: (s) {
                          setState(() => _statusFilter = s);
                          ref.read(tripsProvider.notifier).setFilter(status: s);
                        },
                      ),
                    ],
                  ),
                ),
                const Divider(height: 1),
                // Trips list
                Expanded(
                  child: tripsAsync.when(
                    loading: () =>
                        const Center(child: CircularProgressIndicator()),
                    error: (e, _) => Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.error_outline_rounded,
                              size: 40, color: AppColors.danger),
                          const SizedBox(height: 8),
                          Text('$e',
                              style: const TextStyle(color: AppColors.danger),
                              textAlign: TextAlign.center),
                          const SizedBox(height: 12),
                          FilledButton(
                            onPressed: () => ref.invalidate(tripsProvider),
                            child: const Text('Retry'),
                          ),
                        ],
                      ),
                    ),
                    data: (trips) {
                      final filtered = _filter(trips);
                      if (filtered.isEmpty) {
                        return const Center(
                          child: Text('No trips found',
                              style: TextStyle(color: AppColors.brandGray)),
                        );
                      }
                      return RefreshIndicator(
                        onRefresh: () =>
                            ref.refresh(tripsProvider.future),
                        child: ListView.builder(
                          padding: const EdgeInsets.only(top: 8, bottom: 16),
                          itemCount: filtered.length,
                          itemBuilder: (_, i) {
                            final trip = filtered[i];
                            return TripCard(
                              trip: trip,
                              selected: _selectedTripId == trip.id,
                              onTap: () =>
                                  setState(() => _selectedTripId = trip.id),
                            );
                          },
                        ),
                      );
                    },
                  ),
                ),
              ],
            ),
          ),
          const VerticalDivider(width: 1),
          // ── Right Panel ──────────────────────────────────────────────────
          Expanded(
            child: _selectedTripId != null
                ? TripDetailWorkspace(tripId: _selectedTripId!)
                : const _EmptyDetailState(),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final created = await Navigator.of(context).push<bool>(
            MaterialPageRoute(
              builder: (_) => TripManifestFormPage(
                serviceType: widget.serviceType ?? TripServiceType.charter,
              ),
            ),
          );
          if (created == true) {
            ref.invalidate(tripsProvider);
          }
        },
        backgroundColor: widget.serviceType == TripServiceType.community
            ? const Color(0xFF0F766E)
            : AppColors.primary,
        icon: const Icon(Icons.add_rounded),
        label: const Text('New Trip'),
      ),
    );
  }

  List<Trip> _filter(List<Trip> all) {
    var list = all;
    if (_search.isNotEmpty) {
      final q = _search.toLowerCase();
      list = list.where((t) {
        final route = '${t.firstStopLocation ?? ''} ${t.lastStopLocation ?? ''}'
            .toLowerCase();
        final po = (t.purchaseOrderNumber ?? '').toLowerCase();
        return route.contains(q) || po.contains(q);
      }).toList();
    }
    return list;
  }
}

class _EmptyDetailState extends StatelessWidget {
  const _EmptyDetailState();

  @override
  Widget build(BuildContext context) => const Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.route_rounded, size: 64, color: Color(0xFFE5E7EB)),
            SizedBox(height: 16),
            Text(
              'Select a trip to view details',
              style: TextStyle(
                fontSize: 16,
                color: AppColors.brandGray,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      );
}

class _StatusFilterBar extends StatelessWidget {
  final TripStatus? selected;
  final ValueChanged<TripStatus?> onChanged;

  const _StatusFilterBar({required this.selected, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          _Chip(label: 'All', selected: selected == null, onTap: () => onChanged(null)),
          const SizedBox(width: 6),
          _Chip(
              label: 'Scheduled',
              selected: selected == TripStatus.scheduled,
              onTap: () => onChanged(TripStatus.scheduled)),
          const SizedBox(width: 6),
          _Chip(
              label: 'Dispatched',
              selected: selected == TripStatus.dispatched,
              onTap: () => onChanged(TripStatus.dispatched)),
          const SizedBox(width: 6),
          _Chip(
              label: 'En Route',
              selected: selected == TripStatus.enRoute,
              onTap: () => onChanged(TripStatus.enRoute)),
          const SizedBox(width: 6),
          _Chip(
              label: 'Completed',
              selected: selected == TripStatus.completed,
              onTap: () => onChanged(TripStatus.completed)),
        ],
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback onTap;
  const _Chip({required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
          decoration: BoxDecoration(
            color: selected ? AppColors.primary : const Color(0xFFF3F4F6),
            borderRadius: BorderRadius.circular(20),
          ),
          child: Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: selected ? Colors.white : AppColors.textSecondary,
            ),
          ),
        ),
      );
}
