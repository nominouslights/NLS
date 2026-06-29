import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../../../core/theme/app_colors.dart';
import '../../domain/entities/client.dart';
import '../../domain/usecases/update_client_billing_rates_usecase.dart';
import '../providers/client_detail_provider.dart';

class BillingInfoPanel extends ConsumerStatefulWidget {
  final Client client;
  final String clientId;
  const BillingInfoPanel({
    super.key,
    required this.client,
    required this.clientId,
  });

  @override
  ConsumerState<BillingInfoPanel> createState() => _BillingInfoPanelState();
}

class _BillingInfoPanelState extends ConsumerState<BillingInfoPanel> {
  late final TextEditingController _oneWayCtrl;
  late final TextEditingController _roundTripCtrl;
  late final TextEditingController _cargoCtrl;
  bool _savingRates = false;

  @override
  void initState() {
    super.initState();
    _oneWayCtrl = TextEditingController(
      text: widget.client.runRateOneWay?.toStringAsFixed(2) ?? '',
    );
    _roundTripCtrl = TextEditingController(
      text: widget.client.runRateRoundTrip?.toStringAsFixed(2) ?? '',
    );
    _cargoCtrl = TextEditingController(
      text: widget.client.cargoRatePerKg?.toStringAsFixed(2) ?? '',
    );
  }

  @override
  void dispose() {
    _oneWayCtrl.dispose();
    _roundTripCtrl.dispose();
    _cargoCtrl.dispose();
    super.dispose();
  }

  Future<void> _saveRates() async {
    final oneWay = double.tryParse(_oneWayCtrl.text.trim());
    final roundTrip = double.tryParse(_roundTripCtrl.text.trim());
    final cargo = double.tryParse(_cargoCtrl.text.trim());

    setState(() => _savingRates = true);
    try {
      final result = await sl<UpdateClientBillingRatesUseCase>()(
        UpdateClientBillingRatesParams(
          clientId: widget.clientId,
          oneWayRate: oneWay,
          roundTripRate: roundTrip,
          cargoRatePerKg: cargo,
        ),
      );
      result.fold(
        (failure) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(failure.message),
                backgroundColor: AppColors.danger,
              ),
            );
          }
        },
        (_) {
          ref.invalidate(clientDetailProvider(widget.clientId));
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Billing rates saved.'),
                backgroundColor: AppColors.success,
              ),
            );
          }
        },
      );
    } finally {
      if (mounted) setState(() => _savingRates = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final client = widget.client;

    return Container(
      margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE5E7EB)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _PanelHeader(title: 'Billing', icon: Icons.receipt_long_outlined),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _BillingRow(
                  label: 'GST/HST Number',
                  value: client.gstHstNumber?.isEmpty ?? true
                      ? '—'
                      : (client.gstHstNumber ?? '—'),
                ),
                const SizedBox(height: 8),
                _BillingRow(
                  label: 'Payment Method',
                  value: client.preferredPaymentMethod,
                ),
                const SizedBox(height: 8),
                _BillingRow(
                  label: 'Payment Terms',
                  value: 'Net ${client.netPaymentTerms}',
                ),
                const SizedBox(height: 8),
                _BillingRow(
                  label: 'Outstanding Balance',
                  value: '\$${client.outstandingBalance.toStringAsFixed(2)}',
                  valueStyle: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                    color: client.outstandingBalance > 0
                        ? AppColors.danger
                        : AppColors.success,
                  ),
                ),
                const SizedBox(height: 16),
                const Divider(color: Color(0xFFF3F4F6)),
                const SizedBox(height: 12),
                // Billing rates subsection
                const Text(
                  'BILLING RATES',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: AppColors.brandGray,
                    letterSpacing: 0.6,
                  ),
                ),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _oneWayCtrl,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        inputFormatters: [
                          FilteringTextInputFormatter.allow(
                            RegExp(r'^\d*\.?\d*'),
                          ),
                        ],
                        decoration: _rateDec('One-Way Rate (\$)'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: TextField(
                        controller: _roundTripCtrl,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        inputFormatters: [
                          FilteringTextInputFormatter.allow(
                            RegExp(r'^\d*\.?\d*'),
                          ),
                        ],
                        decoration: _rateDec('Round-Trip Rate (\$)'),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: _cargoCtrl,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        inputFormatters: [
                          FilteringTextInputFormatter.allow(
                            RegExp(r'^\d*\.?\d*'),
                          ),
                        ],
                        decoration: _rateDec('Cargo Rate (\$/kg)'),
                      ),
                    ),
                    const SizedBox(width: 8),
                    const Expanded(child: SizedBox()),
                  ],
                ),
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerRight,
                  child: FilledButton(
                    onPressed: _savingRates ? null : _saveRates,
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 10,
                      ),
                      minimumSize: Size.zero,
                      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                    ),
                    child: _savingRates
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text(
                            'Save Rates',
                            style: TextStyle(fontSize: 13),
                          ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  InputDecoration _rateDec(String label) => InputDecoration(
        labelText: label,
        isDense: true,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 12,
          vertical: 10,
        ),
      );
}

class _BillingRow extends StatelessWidget {
  final String label;
  final String value;
  final TextStyle? valueStyle;

  const _BillingRow({
    required this.label,
    required this.value,
    this.valueStyle,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 13, color: AppColors.brandGray),
        ),
        const Spacer(),
        Text(
          value,
          style: valueStyle ??
              const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: Color(0xFF111827),
              ),
        ),
      ],
    );
  }
}

class _PanelHeader extends StatelessWidget {
  final String title;
  final IconData icon;
  const _PanelHeader({required this.title, required this.icon});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 12),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColors.brandGray),
          const SizedBox(width: 8),
          Text(
            title,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w700,
              color: Color(0xFF374151),
              letterSpacing: 0.5,
            ),
          ),
        ],
      ),
    );
  }
}
