import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/saved_location.dart';
import '../providers/locations_provider.dart';
import 'saved_location_form_sheet.dart';

class SavedLocationsPage extends ConsumerStatefulWidget {
  const SavedLocationsPage({super.key});

  @override
  ConsumerState<SavedLocationsPage> createState() => _SavedLocationsPageState();
}

class _SavedLocationsPageState extends ConsumerState<SavedLocationsPage> {
  String _search = '';

  @override
  Widget build(BuildContext context) {
    final locationsAsync = ref.watch(locationsProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          Container(
            color: Colors.white,
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
            child: TextField(
              decoration: InputDecoration(
                hintText: 'Search by name or address…',
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
                contentPadding: const EdgeInsets.symmetric(vertical: 10),
              ),
              onChanged: (v) => setState(() => _search = v),
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: locationsAsync.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (e, _) => Center(
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Icons.error_outline_rounded,
                        size: 48, color: AppColors.danger),
                    const SizedBox(height: 12),
                    Text('$e',
                        style: const TextStyle(color: AppColors.danger)),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => ref.invalidate(locationsProvider),
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
              data: (locations) {
                final filtered = _filter(locations);
                if (locations.isEmpty) {
                  return Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.bookmark_border_rounded,
                            size: 56,
                            color:
                                AppColors.brandGray.withValues(alpha: 0.4)),
                        const SizedBox(height: 12),
                        const Text(
                          'No saved locations yet',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w600,
                            color: AppColors.brandGray,
                          ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'Tap + to add a location you use often.',
                          style: TextStyle(
                              fontSize: 13, color: AppColors.brandGray),
                        ),
                      ],
                    ),
                  );
                }
                if (filtered.isEmpty) {
                  return const Center(
                    child: Text('No locations match your search.',
                        style: TextStyle(color: AppColors.brandGray)),
                  );
                }
                return RefreshIndicator(
                  onRefresh: () =>
                      ref.read(locationsProvider.notifier).refresh(),
                  child: ListView.builder(
                    padding:
                        const EdgeInsets.only(top: 8, bottom: 100),
                    itemCount: filtered.length,
                    itemBuilder: (_, i) => _LocationTile(
                      location: filtered[i],
                      onEdit: () => _openForm(context, filtered[i]),
                      onDelete: () =>
                          _confirmDelete(context, filtered[i]),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _openForm(context, null),
        backgroundColor: AppColors.primary,
        icon: const Icon(Icons.add_rounded),
        label: const Text('Add Location'),
      ),
    );
  }

  List<SavedLocation> _filter(List<SavedLocation> all) {
    if (_search.isEmpty) return all;
    final q = _search.toLowerCase();
    return all
        .where((l) =>
            l.name.toLowerCase().contains(q) ||
            (l.address?.toLowerCase().contains(q) ?? false))
        .toList();
  }

  Future<void> _openForm(BuildContext context, SavedLocation? loc) async {
    await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => SavedLocationFormSheet(location: loc),
    );
  }

  Future<void> _confirmDelete(
      BuildContext context, SavedLocation loc) async {
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Location'),
        content: Text(
            'Delete "${loc.name}"? This cannot be undone.'),
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
            .read(locationsProvider.notifier)
            .deleteLocation(loc.id);
        if (mounted) {
          messenger.showSnackBar(
            SnackBar(content: Text('"${loc.name}" deleted')),
          );
        }
      } catch (e) {
        if (mounted) {
          messenger.showSnackBar(
            SnackBar(
              content: Text('Failed to delete: $e'),
              backgroundColor: AppColors.danger,
            ),
          );
        }
      }
    }
  }
}

// ── Location tile ─────────────────────────────────────────────────────────────

class _LocationTile extends StatelessWidget {
  final SavedLocation location;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _LocationTile({
    required this.location,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: ListTile(
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(10),
          ),
          child: const Icon(Icons.place_rounded,
              color: AppColors.primary, size: 20),
        ),
        title: Text(
          location.name,
          style: const TextStyle(
            fontWeight: FontWeight.w600,
            fontSize: 14,
            color: Color(0xFF111827),
          ),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (location.address != null && location.address!.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(
                  location.address!,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.brandGray),
                ),
              ),
            if (location.hasCoordinates)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Container(
                  padding: const EdgeInsets.symmetric(
                      horizontal: 6, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppColors.secondary.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(
                        color: AppColors.secondary.withValues(alpha: 0.3)),
                  ),
                  child: Text(
                    '${location.latitude!.toStringAsFixed(4)}, ${location.longitude!.toStringAsFixed(4)}',
                    style: const TextStyle(
                      fontSize: 11,
                      color: AppColors.secondary,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
              ),
          ],
        ),
        trailing: PopupMenuButton<_Action>(
          icon: const Icon(Icons.more_vert_rounded,
              color: AppColors.brandGray, size: 20),
          onSelected: (action) {
            if (action == _Action.edit) onEdit();
            if (action == _Action.delete) onDelete();
          },
          itemBuilder: (_) => [
            const PopupMenuItem(
              value: _Action.edit,
              child: Row(children: [
                Icon(Icons.edit_outlined, size: 16),
                SizedBox(width: 8),
                Text('Edit'),
              ]),
            ),
            const PopupMenuItem(
              value: _Action.delete,
              child: Row(children: [
                Icon(Icons.delete_outline_rounded,
                    size: 16, color: AppColors.danger),
                SizedBox(width: 8),
                Text('Delete',
                    style: TextStyle(color: AppColors.danger)),
              ]),
            ),
          ],
        ),
      ),
    );
  }
}

enum _Action { edit, delete }
