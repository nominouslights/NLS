import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client_email_template.dart';
import '../../domain/repositories/i_client_repository.dart';
import '../providers/client_email_templates_provider.dart';

const _kPlaceholders = <String>[
  '{{TripDate}}',
  '{{DepartureTime}}',
  '{{ArrivalTime}}',
  '{{PickupLocation}}',
  '{{Destination}}',
  '{{Route}}',
  '{{Status}}',
  '{{StopLocation}}',
  '{{PassengerName}}',
  '{{PassengerNames}}',
  '{{ClientName}}',
];

class ClientEmailTemplatesSection extends ConsumerWidget {
  final String clientId;
  const ClientEmailTemplatesSection({super.key, required this.clientId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final templatesAsync = ref.watch(clientEmailTemplatesProvider(clientId));

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Email Templates',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: Color(0xFF111827),
            ),
          ),
          const SizedBox(height: 4),
          const Text(
            'Customize the emails sent for this client. Use the placeholders '
            'below; they are filled in automatically from the trip details.',
            style: TextStyle(fontSize: 12, color: AppColors.brandGray),
          ),
          const SizedBox(height: 12),
          _PlaceholderLegend(),
          const SizedBox(height: 16),
          templatesAsync.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(40),
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (e, _) => Padding(
              padding: const EdgeInsets.all(20),
              child: Text('Failed to load templates: $e',
                  style: const TextStyle(color: AppColors.danger)),
            ),
            data: (templates) {
              final byType = {for (final t in templates) t.type: t};
              return Column(
                children: ClientEmailTemplateType.values.map((type) {
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: _TemplateEditorCard(
                      clientId: clientId,
                      type: type,
                      existing: byType[type],
                    ),
                  );
                }).toList(),
              );
            },
          ),
        ],
      ),
    );
  }
}

class _PlaceholderLegend extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFF9FAFB),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Wrap(
        spacing: 6,
        runSpacing: 6,
        children: _kPlaceholders
            .map((p) => Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: const Color(0xFF0F766E).withValues(alpha: 0.08),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    p,
                    style: const TextStyle(
                      fontSize: 11,
                      fontFamily: 'monospace',
                      color: Color(0xFF0F766E),
                    ),
                  ),
                ))
            .toList(),
      ),
    );
  }
}

class _TemplateEditorCard extends ConsumerStatefulWidget {
  final String clientId;
  final ClientEmailTemplateType type;
  final ClientEmailTemplate? existing;

  const _TemplateEditorCard({
    required this.clientId,
    required this.type,
    required this.existing,
  });

  @override
  ConsumerState<_TemplateEditorCard> createState() =>
      _TemplateEditorCardState();
}

class _TemplateEditorCardState extends ConsumerState<_TemplateEditorCard> {
  late final TextEditingController _subjectCtrl;
  late final TextEditingController _bodyCtrl;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _subjectCtrl = TextEditingController(text: widget.existing?.subject ?? '');
    _bodyCtrl = TextEditingController(text: widget.existing?.body ?? '');
  }

  @override
  void dispose() {
    _subjectCtrl.dispose();
    _bodyCtrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_subjectCtrl.text.trim().isEmpty || _bodyCtrl.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Subject and body are required.'),
          backgroundColor: AppColors.danger,
        ),
      );
      return;
    }

    setState(() => _saving = true);
    try {
      await ref
          .read(clientEmailTemplatesProvider(widget.clientId).notifier)
          .upsert(UpsertEmailTemplateParams(
            clientId: widget.clientId,
            type: widget.type,
            subject: _subjectCtrl.text.trim(),
            body: _bodyCtrl.text,
          ));
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Template saved.'),
            backgroundColor: Color(0xFF059669),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('$e'), backgroundColor: AppColors.danger),
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.mail_outline_rounded,
                  size: 16, color: Color(0xFF0F766E)),
              const SizedBox(width: 8),
              Text(
                widget.type.label,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const Spacer(),
              if (widget.existing == null)
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: AppColors.warning.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: const Text(
                    'Not configured',
                    style: TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                      color: AppColors.warning,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _subjectCtrl,
            decoration: const InputDecoration(
              labelText: 'Subject',
              border: OutlineInputBorder(),
              isDense: true,
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _bodyCtrl,
            minLines: 6,
            maxLines: 16,
            decoration: const InputDecoration(
              labelText: 'Body',
              border: OutlineInputBorder(),
              alignLabelWithHint: true,
            ),
          ),
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              onPressed: _saving ? null : _save,
              icon: _saving
                  ? const SizedBox(
                      width: 14,
                      height: 14,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.save_rounded, size: 16),
              label: const Text('Save'),
              style: FilledButton.styleFrom(
                  backgroundColor: const Color(0xFF0F766E)),
            ),
          ),
        ],
      ),
    );
  }
}
