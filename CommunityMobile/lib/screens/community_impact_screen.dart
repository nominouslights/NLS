import 'package:flutter/material.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/progress_bar.dart';
import '../widgets/section_card.dart';
import '../widgets/segmented_tabs.dart';

/// Screen 6 — Community Impact (shell tab).
class CommunityImpactScreen extends StatefulWidget {
  const CommunityImpactScreen({super.key});

  @override
  State<CommunityImpactScreen> createState() => _CommunityImpactScreenState();
}

class _CommunityImpactScreenState extends State<CommunityImpactScreen> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final projects =
        communityProjects.where((p) => p.active == (_tab == 0)).toList();
    return SafeArea(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Center(child: Text('Community Impact', style: NLText.screenTitle)),
          const SizedBox(height: 14),
          SegmentedTabs(
            labels: const ['Active Projects', 'Past Projects'],
            selected: _tab,
            onChanged: (i) => setState(() => _tab = i),
          ),
          const SizedBox(height: 14),
          if (projects.isEmpty)
            SectionCard(
              padding: const EdgeInsets.symmetric(vertical: 32),
              child: Column(
                children: [
                  const Icon(Icons.history, size: 34, color: NLColors.textMuted),
                  const SizedBox(height: 8),
                  Text('Past projects will appear here.',
                      style: NLText.muted, textAlign: TextAlign.center),
                ],
              ),
            )
          else
            for (final p in projects) ...[
              _projectCard(p),
              const SizedBox(height: 12),
            ],
          const SizedBox(height: 6),
          NLButton.gold(label: 'Donate Points', icon: Icons.volunteer_activism_outlined, onPressed: () {}),
        ],
      ),
    );
  }

  Widget _projectCard(CommunityProject p) {
    return SectionCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(p.title, style: NLText.cardTitle),
                    const SizedBox(height: 4),
                    Text(p.blurb, style: NLText.muted),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              IconDisc(p.icon, color: NLColors.gold, size: 44),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(child: NLProgressBar(p.percent / 100)),
              const SizedBox(width: 10),
              Text('${p.percent}%',
                  style: NLText.body.copyWith(fontWeight: FontWeight.w700)),
            ],
          ),
        ],
      ),
    );
  }
}
