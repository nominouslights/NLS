import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../data/mock_data.dart';
import '../theme/nl_theme.dart';
import '../widgets/nl_button.dart';
import '../widgets/section_card.dart';

/// Screen 9 — Invite & Earn.
class InviteEarnScreen extends StatelessWidget {
  const InviteEarnScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Invite & Earn')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
        children: [
          // Placeholder for the people illustration.
          Center(
            child: Container(
              width: 120,
              height: 120,
              decoration: BoxDecoration(
                color: NLColors.primary.withValues(alpha: .08),
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.diversity_3, size: 60, color: NLColors.primary),
            ),
          ),
          const SizedBox(height: 16),
          Text(
            'Invite friends & family.\nEveryone benefits!',
            textAlign: TextAlign.center,
            style: NLText.screenTitle.copyWith(fontSize: 24),
          ),
          const SizedBox(height: 18),
          Row(
            children: [
              Expanded(
                child: _bonusCard('You get', inviteYouGet, 'for each friend'),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _bonusCard('They get', inviteTheyGet, 'on their first trip'),
              ),
            ],
          ),
          const SizedBox(height: 18),
          const SectionLabel('Your Invite Link'),
          SectionCard(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    inviteLink,
                    style: NLText.muted.copyWith(fontSize: 12.5),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                IconButton(
                  onPressed: () {
                    Clipboard.setData(const ClipboardData(text: inviteLink));
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Invite link copied')),
                    );
                  },
                  icon: const Icon(Icons.copy, size: 18, color: NLColors.primary),
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          NLButton.gold(label: 'Share Invite Link', icon: Icons.ios_share, onPressed: () {}),
          const SizedBox(height: 18),
          SectionCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(Icons.info_outline, size: 18, color: NLColors.textMuted),
                    const SizedBox(width: 8),
                    Text('How it works', style: NLText.cardTitle),
                  ],
                ),
                const SizedBox(height: 12),
                for (final p in keyPrinciples)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(p.icon, size: 18, color: NLColors.primary),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text.rich(
                            TextSpan(
                              text: '${p.title} — ',
                              style: NLText.body.copyWith(fontWeight: FontWeight.w600),
                              children: [
                                TextSpan(text: p.detail, style: NLText.muted),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _bonusCard(String who, int pts, String condition) {
    return SectionCard(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
      child: Column(
        children: [
          Text(who, style: NLText.muted),
          const SizedBox(height: 4),
          Text('$pts pts',
              style: NLText.bigNumber.copyWith(fontSize: 26, color: NLColors.goldTextDark)),
          const SizedBox(height: 4),
          Text(condition, style: NLText.muted, textAlign: TextAlign.center),
        ],
      ),
    );
  }
}
