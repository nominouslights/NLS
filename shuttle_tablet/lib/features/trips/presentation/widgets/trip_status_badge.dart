import 'package:flutter/material.dart';
import '../../domain/entities/trip.dart';

class TripStatusBadge extends StatelessWidget {
  final TripStatus status;
  const TripStatusBadge(this.status, {super.key});

  @override
  Widget build(BuildContext context) {
    final (label, bg, fg) = _style(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: fg,
          letterSpacing: 0.3,
        ),
      ),
    );
  }

  static (String, Color, Color) _style(TripStatus s) => switch (s) {
        TripStatus.scheduled => ('Scheduled', const Color(0xFFEBF0FA), const Color(0xFF3B5998)),
        TripStatus.dispatched => ('Dispatched', const Color(0xFFDEEDFF), const Color(0xFF005493)),
        TripStatus.enRoute => ('En Route', const Color(0xFFFFF3CD), const Color(0xFF856404)),
        TripStatus.completed => ('Completed', const Color(0xFFD4EDDA), const Color(0xFF155724)),
        TripStatus.cancelled => ('Cancelled', const Color(0xFFF8D7DA), const Color(0xFF721C24)),
      };

  /// Returns the background colour for a given status — useful in other widgets.
  static Color colorFor(TripStatus s) => _style(s).$2;
}
