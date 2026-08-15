import 'package:flutter/material.dart';

import '../screens/community_impact_screen.dart';
import '../screens/home_screen.dart';
import '../screens/more_screen.dart';
import '../screens/my_trips_screen.dart';
import '../screens/wallet_screen.dart';
import '../theme/nl_theme.dart';

/// 5-tab shell matching the board's bottom nav: Home, Trips, Wallet, Impact,
/// More. Detail screens (Book a Trip, Trip Details, Rewards, …) are pushed on
/// top of the shell.
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final tabs = [
      HomeScreen(onNavigateTab: (i) => setState(() => _tab = i)),
      const MyTripsScreen(),
      const WalletScreen(),
      const CommunityImpactScreen(),
      const MoreScreen(),
    ];
    return Scaffold(
      body: IndexedStack(index: _tab, children: tabs),
      bottomNavigationBar: NavigationBarTheme(
        data: NavigationBarThemeData(
          backgroundColor: NLColors.card,
          indicatorColor: NLColors.primary.withValues(alpha: .12),
          height: 66,
          labelTextStyle: WidgetStateProperty.resolveWith(
            (states) => TextStyle(
              fontFamily: NLFonts.body,
              fontSize: 11.5,
              fontWeight: FontWeight.w600,
              color: states.contains(WidgetState.selected)
                  ? NLColors.primary
                  : NLColors.textMuted,
            ),
          ),
          iconTheme: WidgetStateProperty.resolveWith(
            (states) => IconThemeData(
              size: 24,
              color: states.contains(WidgetState.selected)
                  ? NLColors.primary
                  : NLColors.textMuted,
            ),
          ),
        ),
        child: NavigationBar(
          selectedIndex: _tab,
          onDestinationSelected: (i) => setState(() => _tab = i),
          destinations: const [
            NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home), label: 'Home'),
            NavigationDestination(
                icon: Icon(Icons.directions_bus_outlined),
                selectedIcon: Icon(Icons.directions_bus),
                label: 'Trips'),
            NavigationDestination(
                icon: Icon(Icons.account_balance_wallet_outlined),
                selectedIcon: Icon(Icons.account_balance_wallet),
                label: 'Wallet'),
            NavigationDestination(
                icon: Icon(Icons.favorite_outline), selectedIcon: Icon(Icons.favorite), label: 'Impact'),
            NavigationDestination(icon: Icon(Icons.menu), label: 'More'),
          ],
        ),
      ),
    );
  }
}
