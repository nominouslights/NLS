import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../theme/nl_theme.dart';
import 'section_card.dart';

/// The owner's published Privacy Policy and End-User Licence Agreement.
/// One document, two anchors — the only place these URLs live in the app.
abstract class LegalLinks {
  static const _base =
      'https://app.northernlinkshuttleandcargo.com/legal/policies.html';
  static final privacyPolicy = Uri.parse('$_base#privacy');
  static final licenceAgreement = Uri.parse('$_base#eula');
}

/// Opens [uri] in the platform browser. If nothing can open it, shows the
/// address so the rider can still reach it by hand.
Future<void> openLegalLink(BuildContext context, Uri uri) async {
  final messenger = ScaffoldMessenger.of(context);
  var opened = false;
  try {
    opened = await launchUrl(uri, mode: LaunchMode.externalApplication);
  } catch (_) {
    opened = false;
  }
  if (!opened) {
    messenger.showSnackBar(
      SnackBar(content: Text('Could not open the page. Visit $uri')),
    );
  }
}

/// "Legal" card for the profile screen: Privacy Policy and Licence Agreement
/// rows, each opening the published document.
class LegalLinksCard extends StatelessWidget {
  const LegalLinksCard({super.key});

  @override
  Widget build(BuildContext context) {
    final rows = <(IconData, String, Uri)>[
      (Icons.privacy_tip_outlined, 'Privacy Policy', LegalLinks.privacyPolicy),
      (
        Icons.description_outlined,
        'Licence Agreement',
        LegalLinks.licenceAgreement,
      ),
    ];
    return SectionCard(
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
      child: Column(
        children: [
          for (var i = 0; i < rows.length; i++) ...[
            InkWell(
              borderRadius: BorderRadius.circular(NLRadii.button),
              onTap: () => openLegalLink(context, rows[i].$3),
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 12,
                ),
                child: Row(
                  children: [
                    Icon(rows[i].$1, size: 20, color: NLColors.primary),
                    const SizedBox(width: 12),
                    Expanded(child: Text(rows[i].$2, style: NLText.body)),
                    const Icon(
                      Icons.open_in_new,
                      size: 18,
                      color: NLColors.textMuted,
                    ),
                  ],
                ),
              ),
            ),
            if (i < rows.length - 1) const Divider(height: 1),
          ],
        ],
      ),
    );
  }
}

/// Small consent line for booking: "By booking, you agree to the Licence
/// Agreement and acknowledge the Privacy Policy." Both names are tappable.
class LegalConsentText extends StatefulWidget {
  const LegalConsentText({super.key});

  @override
  State<LegalConsentText> createState() => _LegalConsentTextState();
}

class _LegalConsentTextState extends State<LegalConsentText> {
  late final TapGestureRecognizer _eula;
  late final TapGestureRecognizer _privacy;

  @override
  void initState() {
    super.initState();
    _eula =
        TapGestureRecognizer()
          ..onTap = () => openLegalLink(context, LegalLinks.licenceAgreement);
    _privacy =
        TapGestureRecognizer()
          ..onTap = () => openLegalLink(context, LegalLinks.privacyPolicy);
  }

  @override
  void dispose() {
    _eula.dispose();
    _privacy.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final link = NLText.muted.copyWith(
      color: NLColors.primary,
      fontWeight: FontWeight.w600,
      decoration: TextDecoration.underline,
      decorationColor: NLColors.primary,
    );
    return Text.rich(
      TextSpan(
        style: NLText.muted.copyWith(fontSize: 11.5),
        children: [
          const TextSpan(text: 'By booking, you agree to the '),
          TextSpan(
            text: 'Licence Agreement',
            style: link.copyWith(fontSize: 11.5),
            recognizer: _eula,
          ),
          const TextSpan(text: ' and acknowledge the '),
          TextSpan(
            text: 'Privacy Policy',
            style: link.copyWith(fontSize: 11.5),
            recognizer: _privacy,
          ),
          const TextSpan(text: '.'),
        ],
      ),
      textAlign: TextAlign.center,
    );
  }
}
