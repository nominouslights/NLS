import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/points_text.dart';

/// Screen 8 — Gift Basket Details. The one dark-navy screen on the board.
class GiftBasketScreen extends StatefulWidget {
  final Reward reward;

  const GiftBasketScreen({super.key, required this.reward});

  @override
  State<GiftBasketScreen> createState() => _GiftBasketScreenState();
}

class _GiftBasketScreenState extends State<GiftBasketScreen> {
  bool _favorite = false;

  @override
  Widget build(BuildContext context) {
    final r = widget.reward;
    return Scaffold(
      backgroundColor: NLColors.navyDark,
      appBar: AppBar(
        backgroundColor: NLColors.navyDark,
        foregroundColor: Colors.white,
        actions: [
          IconButton(
            onPressed: () => setState(() => _favorite = !_favorite),
            icon: Icon(
              _favorite ? Icons.favorite : Icons.favorite_outline,
              color: _favorite ? NLColors.goldLight : Colors.white,
            ),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 4, 20, 28),
        children: [
          // Placeholder hero: gradient block + icon instead of the photo.
          Container(
            height: 220,
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [NLColors.navyMid, NLColors.navyDark.withValues(alpha: .4)],
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
              ),
              borderRadius: BorderRadius.circular(NLRadii.card),
              border: Border.all(color: Colors.white.withValues(alpha: .12)),
            ),
            child: Icon(r.icon, size: 88, color: NLColors.goldLight),
          ),
          const SizedBox(height: 20),
          Text(
            r.name,
            style: NLText.screenTitle.copyWith(color: Colors.white, fontSize: 26),
          ),
          const SizedBox(height: 6),
          PointsCost(r.points, fontSize: 22, onDark: true),
          const SizedBox(height: 18),
          Text('Includes:',
              style: NLText.body.copyWith(color: Colors.white, fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          for (final item in r.includes)
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Row(
                children: [
                  const Icon(Icons.circle, size: 6, color: NLColors.goldLight),
                  const SizedBox(width: 10),
                  Text(item, style: NLText.body.copyWith(color: NLColors.textFaintOnDark)),
                ],
              ),
            ),
          const SizedBox(height: 14),
          Text('Quantity: ${r.quantity}',
              style: NLText.muted.copyWith(color: NLColors.textFaintOnDark)),
          const SizedBox(height: 20),
          NLButton.gold(label: 'Redeem Now', onPressed: () {}),
        ],
      ),
    );
  }
}
