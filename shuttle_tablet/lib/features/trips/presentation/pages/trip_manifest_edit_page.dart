import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../providers/trips_provider.dart';
import 'trip_manifest_form_page.dart';

/// Loads a trip by id then renders the manifest form in edit mode.
class TripManifestEditPage extends ConsumerWidget {
  final String tripId;
  final int initialStep;

  const TripManifestEditPage({
    super.key,
    required this.tripId,
    this.initialStep = 0,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final tripAsync = ref.watch(tripDetailProvider(tripId));

    return tripAsync.when(
      loading: () => const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      ),
      error: (e, _) => Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline_rounded,
                  size: 48, color: AppColors.danger),
              const SizedBox(height: 12),
              Text('$e', style: const TextStyle(color: AppColors.danger)),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => ref.invalidate(tripDetailProvider(tripId)),
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      ),
      data: (trip) => TripManifestFormPage(
        key: ValueKey(trip.id),
        trip: trip,
        serviceType: trip.serviceType,
        initialStep: initialStep,
      ),
    );
  }
}
