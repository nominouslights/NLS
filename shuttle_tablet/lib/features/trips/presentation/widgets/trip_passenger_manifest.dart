import 'package:flutter/material.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/trip.dart';
import '../../domain/entities/trip_passenger.dart';
import '../../domain/repositories/i_trip_repository.dart';
import '../../domain/usecases/remove_passenger_usecase.dart';
import '../../domain/usecases/send_passenger_confirmation_usecase.dart';
import '../../domain/usecases/update_passenger_boarding_status_usecase.dart';
import '../../domain/usecases/update_passenger_payment_status_usecase.dart';
import 'add_passenger_sheet.dart';

class TripPassengerManifest extends StatelessWidget {
  final Trip trip;
  final VoidCallback onRefresh;

  const TripPassengerManifest({
    super.key,
    required this.trip,
    required this.onRefresh,
  });

  bool get _isLocked => trip.status == TripStatus.enRoute;

  bool get _isTerminal =>
      trip.status == TripStatus.completed ||
      trip.status == TripStatus.cancelled;

  @override
  Widget build(BuildContext context) {
    final passengers = trip.passengers;
    final active = passengers
        .where((p) => p.paymentStatus != PassengerPaymentStatus.cancelled)
        .toList();
    final capacity = trip.seatCapacity;
    final countLabel = capacity != null
        ? 'Passengers (${active.length} / $capacity)'
        : 'Passengers (${active.length})';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Icon(Icons.people_rounded, size: 16, color: AppColors.primary),
            const SizedBox(width: 6),
            Text(
              countLabel,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: AppColors.textPrimary,
                letterSpacing: 0.3,
              ),
            ),
            const Spacer(),
            if (!_isTerminal) ...[
              if (_isLocked)
                TextButton.icon(
                  onPressed: () => showOverrideAddPassengerConfirm(
                    context,
                    tripId: trip.id,
                    onRefresh: onRefresh,
                    clientId: trip.clientId,
                  ),
                  icon: const Icon(Icons.lock_open_rounded, size: 16),
                  label: const Text('Override Add'),
                  style:
                      TextButton.styleFrom(foregroundColor: AppColors.danger),
                )
              else
                TextButton.icon(
                  onPressed: () => showAddPassengerSheet(
                    context,
                    tripId: trip.id,
                    onRefresh: onRefresh,
                    clientId: trip.clientId,
                  ),
                  icon: const Icon(Icons.person_add_alt_1_rounded, size: 16),
                  label: const Text('Add'),
                  style: TextButton.styleFrom(
                      foregroundColor: const Color(0xFF0F766E)),
                ),
            ],
          ],
        ),
        if (_isLocked && !_isTerminal)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: Row(
              children: [
                const Icon(Icons.lock_rounded,
                    size: 12, color: AppColors.textSecondary),
                const SizedBox(width: 4),
                Text(
                  'Manifest locked — trip is en route',
                  style: TextStyle(
                    fontSize: 11,
                    color: AppColors.textSecondary.withValues(alpha: 0.8),
                  ),
                ),
              ],
            ),
          ),
        const SizedBox(height: 4),
        if (passengers.isEmpty)
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: const Color(0xFFF9FAFB),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFE5E7EB)),
            ),
            child: const Center(
              child: Text(
                'No passengers yet',
                style: TextStyle(color: AppColors.brandGray, fontSize: 13),
              ),
            ),
          )
        else
          ...passengers.map(
            (p) => _PassengerCard(
              passenger: p,
              tripId: trip.id,
              onRefresh: onRefresh,
              readOnly: _isTerminal,
            ),
          ),
      ],
    );
  }
}

class _PassengerCard extends StatelessWidget {
  final TripPassenger passenger;
  final String tripId;
  final VoidCallback onRefresh;
  final bool readOnly;

  const _PassengerCard({
    required this.passenger,
    required this.tripId,
    required this.onRefresh,
    this.readOnly = false,
  });

  @override
  Widget build(BuildContext context) {
    final isOverride = passenger.isAddedAfterDeparture;
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: isOverride
            ? AppColors.danger.withValues(alpha: 0.04)
            : Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: isOverride
              ? AppColors.danger.withValues(alpha: 0.35)
              : const Color(0xFFE5E7EB),
        ),
      ),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: isOverride
                  ? AppColors.danger.withValues(alpha: 0.12)
                  : _statusColor(passenger.paymentStatus).withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: Center(
              child: Text(
                passenger.name.isNotEmpty
                    ? passenger.name[0].toUpperCase()
                    : '?',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: isOverride
                      ? AppColors.danger
                      : _statusColor(passenger.paymentStatus),
                ),
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(
                      passenger.name,
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 14,
                        color: Color(0xFF111827),
                      ),
                    ),
                    if (isOverride) ...[
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 5, vertical: 2),
                        decoration: BoxDecoration(
                          color: AppColors.danger.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: const Text(
                          'LATE ADD',
                          style: TextStyle(
                            fontSize: 9,
                            fontWeight: FontWeight.w800,
                            color: AppColors.danger,
                            letterSpacing: 0.5,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
                if (passenger.phone != null)
                  Text(
                    passenger.phone!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
                if (passenger.email != null)
                  Text(
                    passenger.email!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  )
                else if (passenger.contactInfo != null)
                  Text(
                    passenger.contactInfo!,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textSecondary,
                    ),
                  ),
              ],
            ),
          ),
          if (passenger.seatNumber != null)
            Container(
              margin: const EdgeInsets.only(right: 8),
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(
                color: const Color(0xFFF3F4F6),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                'Seat ${passenger.seatNumber}',
                style: const TextStyle(
                    fontSize: 11, fontWeight: FontWeight.w600),
              ),
            ),
          if (!readOnly) ...[
            _BoardingButton(
              icon: Icons.check_rounded,
              label: 'On',
              active: passenger.boardingStatus == PassengerBoardingStatus.boarded,
              activeColor: AppColors.success,
              onTap: () => _updateBoarding(context, PassengerBoardingStatus.boarded),
            ),
            const SizedBox(width: 4),
            _BoardingButton(
              icon: Icons.close_rounded,
              label: 'NS',
              active: passenger.boardingStatus == PassengerBoardingStatus.noShow,
              activeColor: AppColors.danger,
              onTap: () => _updateBoarding(context, PassengerBoardingStatus.noShow),
            ),
            const SizedBox(width: 6),
          ],
          _StatusBadge(passenger.paymentStatus),
          if (!readOnly) ...[
            const SizedBox(width: 4),
            PopupMenuButton<String>(
              icon: const Icon(Icons.more_vert_rounded,
                  size: 18, color: AppColors.brandGray),
              onSelected: (v) => _handleAction(context, v),
              itemBuilder: (_) => [
                const PopupMenuItem(
                  value: 'send',
                  child: Row(
                    children: [
                      Icon(Icons.mail_outline_rounded,
                          size: 16, color: Color(0xFF0F766E)),
                      SizedBox(width: 8),
                      Text('Send Confirmation'),
                    ],
                  ),
                ),
                const PopupMenuDivider(),
                const PopupMenuItem(
                    value: 'pending', child: Text('Mark Pending')),
                const PopupMenuItem(value: 'paid', child: Text('Mark Paid')),
                const PopupMenuItem(
                    value: 'cancel', child: Text('Cancel Booking')),
                const PopupMenuDivider(),
                const PopupMenuItem(
                  value: 'remove',
                  child: Text('Remove Passenger',
                      style: TextStyle(color: AppColors.danger)),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Future<void> _handleAction(BuildContext context, String action) async {
    if (action == 'send') {
      await _sendConfirmation(context);
      return;
    }

    if (action == 'remove') {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Remove Passenger'),
          content: Text('Remove ${passenger.name} from this trip?'),
          actions: [
            TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
              child: const Text('Remove'),
            ),
          ],
        ),
      );
      if (confirmed != true || !context.mounted) return;
      final result =
          await sl<RemovePassengerUseCase>()(RemovePassengerParams(
        tripId: tripId,
        passengerId: passenger.id,
      ));
      result.fold(
        (f) {
          if (context.mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                  content: Text(f.message),
                  backgroundColor: AppColors.danger),
            );
          }
        },
        (_) => onRefresh(),
      );
      return;
    }

    PassengerPaymentStatus newStatus;
    switch (action) {
      case 'paid':
        newStatus = PassengerPaymentStatus.confirmed;
      case 'cancel':
        newStatus = PassengerPaymentStatus.cancelled;
      default:
        newStatus = PassengerPaymentStatus.tentative;
    }

    final result = await sl<UpdatePassengerPaymentStatusUseCase>()(
      UpdatePassengerPaymentStatusParams(
        tripId: tripId,
        passengerId: passenger.id,
        paymentStatus: newStatus,
      ),
    );
    result.fold(
      (f) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(f.message),
                backgroundColor: AppColors.danger),
          );
        }
      },
      (_) => onRefresh(),
    );
  }

  Future<void> _sendConfirmation(BuildContext context) async {
    final hasEmail = (passenger.email != null && passenger.email!.isNotEmpty) ||
        (passenger.contactInfo != null && passenger.contactInfo!.isNotEmpty);
    if (!hasEmail) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('This passenger has no email address on file.'),
          backgroundColor: AppColors.danger,
        ),
      );
      return;
    }

    final direction = await showDialog<ConfirmationDirection>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Send Shuttle Confirmation'),
        content: Text(
          'Send a confirmation email to ${passenger.name}. '
          'Choose which trip leg to confirm.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Cancel'),
          ),
          OutlinedButton(
            onPressed: () =>
                Navigator.pop(ctx, ConfirmationDirection.inbound),
            child: const Text('Inbound'),
          ),
          FilledButton(
            onPressed: () =>
                Navigator.pop(ctx, ConfirmationDirection.outbound),
            style: FilledButton.styleFrom(
                backgroundColor: const Color(0xFF0F766E)),
            child: const Text('Outbound'),
          ),
        ],
      ),
    );

    if (direction == null || !context.mounted) return;

    final result = await sl<SendPassengerConfirmationUseCase>()(
      SendPassengerConfirmationParams(
        tripId: tripId,
        passengerId: passenger.id,
        direction: direction,
      ),
    );

    if (!context.mounted) return;
    result.fold(
      (f) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(f.message), backgroundColor: AppColors.danger),
      ),
      (_) => ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Confirmation email sent to ${passenger.name}.'),
          backgroundColor: const Color(0xFF059669),
        ),
      ),
    );
  }

  Future<void> _updateBoarding(
      BuildContext context, PassengerBoardingStatus status) async {
    final newStatus = passenger.boardingStatus == status
        ? PassengerBoardingStatus.notBoarded
        : status;
    final result = await sl<UpdatePassengerBoardingStatusUseCase>()(
      UpdatePassengerBoardingStatusParams(
        tripId: tripId,
        passengerId: passenger.id,
        boardingStatus: newStatus,
      ),
    );
    result.fold(
      (f) {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
                content: Text(f.message),
                backgroundColor: AppColors.danger),
          );
        }
      },
      (_) => onRefresh(),
    );
  }

  Color _statusColor(PassengerPaymentStatus status) {
    return switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        const Color(0xFF059669),
      PassengerPaymentStatus.cancelled || PassengerPaymentStatus.released =>
        AppColors.danger,
      _ => const Color(0xFFD97706),
    };
  }
}

class _BoardingButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool active;
  final Color activeColor;
  final VoidCallback onTap;

  const _BoardingButton({
    required this.icon,
    required this.label,
    required this.active,
    required this.activeColor,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          decoration: BoxDecoration(
            color: active ? activeColor.withValues(alpha: 0.12) : Colors.white,
            borderRadius: BorderRadius.circular(6),
            border: Border.all(
              color: active ? activeColor : const Color(0xFFD1D5DB),
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 12, color: active ? activeColor : AppColors.brandGray),
              const SizedBox(width: 3),
              Text(
                label,
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w700,
                  color: active ? activeColor : AppColors.brandGray,
                ),
              ),
            ],
          ),
        ),
      );
}

class _StatusBadge extends StatelessWidget {
  final PassengerPaymentStatus status;
  const _StatusBadge(this.status);

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      PassengerPaymentStatus.confirmed || PassengerPaymentStatus.paid =>
        ('Confirmed', const Color(0xFF059669)),
      PassengerPaymentStatus.tentative || PassengerPaymentStatus.pending =>
        ('Tentative', const Color(0xFFD97706)),
      PassengerPaymentStatus.awaitingPayment =>
        ('Awaiting Payment', const Color(0xFFEA580C)),
      PassengerPaymentStatus.released => ('Released', AppColors.danger),
      PassengerPaymentStatus.cancelled => ('Cancelled', AppColors.danger),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }
}
