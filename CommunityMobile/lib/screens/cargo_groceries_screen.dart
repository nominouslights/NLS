import 'package:flutter/material.dart';

import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/section_card.dart';
import '../widgets/segmented_tabs.dart';

/// Screen 10 — Cargo & Groceries.
class CargoGroceriesScreen extends StatefulWidget {
  const CargoGroceriesScreen({super.key});

  @override
  State<CargoGroceriesScreen> createState() => _CargoGroceriesScreenState();
}

class _CargoGroceriesScreenState extends State<CargoGroceriesScreen> {
  int _tab = 0;

  static const _cargoOptions = <(IconData, String, String)>[
    (Icons.inventory_2_outlined, 'Ship a Package', 'Send packages to your friends & family.'),
    (Icons.local_shipping_outlined, 'My Shipments', 'Track your active shipments.'),
  ];

  static const _groceryOptions = <(IconData, String, String)>[
    (Icons.shopping_basket_outlined, 'Grocery Order', 'Order groceries for pick up or delivery.'),
    (Icons.receipt_long_outlined, 'My Orders', 'Track your grocery orders.'),
  ];

  @override
  Widget build(BuildContext context) {
    final options = _tab == 0 ? _cargoOptions : _groceryOptions;
    return Scaffold(
      appBar: AppBar(title: const Text('Cargo & Groceries')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          SegmentedTabs(
            labels: const ['Cargo', 'Groceries'],
            selected: _tab,
            onChanged: (i) => setState(() => _tab = i),
          ),
          const SizedBox(height: 14),
          for (final (icon, title, subtitle) in options) ...[
            SectionCard(
              onTap: () {},
              child: Row(
                children: [
                  IconDisc(icon, size: 46),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(title, style: NLText.cardTitle),
                        const SizedBox(height: 2),
                        Text(subtitle, style: NLText.muted),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: NLColors.textMuted),
                ],
              ),
            ),
            const SizedBox(height: 12),
          ],
          const SizedBox(height: 8),
          NLButton(label: 'Learn More', onPressed: () {}),
        ],
      ),
    );
  }
}
