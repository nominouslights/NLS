import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/app_mode/app_mode_provider.dart';
import '../../../../core/routing/route_names.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../drivers/presentation/pages/drivers_list_page.dart';
import '../../../locations/presentation/pages/saved_locations_page.dart';
import 'admin_bookings_list_page.dart';
import 'admin_calendar_page.dart';
import 'booking_flow_page.dart';
import 'community_dashboard_page.dart';
import 'my_bookings_page.dart';

class CommunityHomePage extends ConsumerStatefulWidget {
  const CommunityHomePage({super.key});

  @override
  ConsumerState<CommunityHomePage> createState() => _CommunityHomePageState();
}

class _CommunityHomePageState extends ConsumerState<CommunityHomePage> {
  int _adminIndex = 0;
  int _bookingIndex = 0;

  late final List<Widget> _adminPages;

  @override
  void initState() {
    super.initState();
    _adminPages = [
      CommunityDashboardPage(
        onCalendarTap: () => setState(() => _adminIndex = 1),
        onBookingsTap: () => setState(() => _adminIndex = 2),
      ),
      const AdminCalendarPage(),
      const AdminBookingsListPage(),
      const DriversListPage(),
      const SavedLocationsPage(),
    ];
  }

  static const _bookingPages = [
    BookingFlowPage(),
    MyBookingsPage(),
  ];

  @override
  Widget build(BuildContext context) {
    final viewMode = ref.watch(communityViewModeProvider);
    final isAdmin = viewMode == CommunityViewMode.admin;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: PreferredSize(
        preferredSize: const Size.fromHeight(64),
        child: _CommunityAppBar(
          viewMode: viewMode,
          onViewModeChanged: (mode) =>
              ref.read(communityViewModeProvider.notifier).state = mode,
          onSwitchMode: () => context.go(RouteNames.modeSelection),
        ),
      ),
      body: isAdmin
          ? _adminPages[_adminIndex]
          : _bookingPages[_bookingIndex],
      bottomNavigationBar: isAdmin
          ? NavigationBar(
              selectedIndex: _adminIndex,
              onDestinationSelected: (i) => setState(() => _adminIndex = i),
              backgroundColor: Colors.white,
              indicatorColor: AppColors.primary.withValues(alpha: 0.12),
              surfaceTintColor: Colors.transparent,
              shadowColor: Colors.black.withValues(alpha: 0.08),
              elevation: 8,
              destinations: const [
                NavigationDestination(
                  icon: Icon(Icons.dashboard_outlined),
                  selectedIcon:
                      Icon(Icons.dashboard_rounded, color: AppColors.primary),
                  label: 'Dashboard',
                ),
                NavigationDestination(
                  icon: Icon(Icons.calendar_month_outlined),
                  selectedIcon: Icon(Icons.calendar_month_rounded,
                      color: AppColors.primary),
                  label: 'Calendar',
                ),
                NavigationDestination(
                  icon: Icon(Icons.list_alt_outlined),
                  selectedIcon:
                      Icon(Icons.list_alt_rounded, color: AppColors.primary),
                  label: 'Bookings',
                ),
                NavigationDestination(
                  icon: Icon(Icons.people_outline_rounded),
                  selectedIcon:
                      Icon(Icons.people_rounded, color: AppColors.primary),
                  label: 'Drivers',
                ),
                NavigationDestination(
                  icon: Icon(Icons.bookmark_border_rounded),
                  selectedIcon:
                      Icon(Icons.bookmark_rounded, color: AppColors.primary),
                  label: 'Locations',
                ),
              ],
            )
          : NavigationBar(
              selectedIndex: _bookingIndex,
              onDestinationSelected: (i) => setState(() => _bookingIndex = i),
              backgroundColor: Colors.white,
              indicatorColor: AppColors.primary.withValues(alpha: 0.12),
              surfaceTintColor: Colors.transparent,
              shadowColor: Colors.black.withValues(alpha: 0.08),
              elevation: 8,
              destinations: const [
                NavigationDestination(
                  icon: Icon(Icons.directions_bus_outlined),
                  selectedIcon: Icon(Icons.directions_bus_rounded,
                      color: AppColors.primary),
                  label: 'Book a Seat',
                ),
                NavigationDestination(
                  icon: Icon(Icons.confirmation_number_outlined),
                  selectedIcon: Icon(Icons.confirmation_number_rounded,
                      color: AppColors.primary),
                  label: 'My Bookings',
                ),
              ],
            ),
    );
  }
}

class _CommunityAppBar extends StatelessWidget {
  final CommunityViewMode viewMode;
  final ValueChanged<CommunityViewMode> onViewModeChanged;
  final VoidCallback onSwitchMode;

  const _CommunityAppBar({
    required this.viewMode,
    required this.onViewModeChanged,
    required this.onSwitchMode,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 64,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Row(
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: const Color(0xFF0F766E),
              borderRadius: BorderRadius.circular(9),
              boxShadow: [
                BoxShadow(
                  color: const Color(0xFF0F766E).withValues(alpha: 0.3),
                  blurRadius: 16,
                ),
              ],
            ),
            child: const Icon(Icons.people_rounded,
                color: Colors.white, size: 18),
          ),
          const SizedBox(width: 10),
          const Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Community',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.2,
                ),
              ),
              Text(
                'PER-SEAT RUNS',
                style: TextStyle(
                  fontSize: 9,
                  fontWeight: FontWeight.w500,
                  color: AppColors.brandGray,
                  letterSpacing: 0.8,
                  height: 1.2,
                ),
              ),
            ],
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Center(
              child: SegmentedButton<CommunityViewMode>(
                selected: {viewMode},
                onSelectionChanged: (s) => onViewModeChanged(s.first),
                style: SegmentedButton.styleFrom(
                  selectedBackgroundColor:
                      AppColors.primary.withValues(alpha: 0.1),
                  selectedForegroundColor: AppColors.primary,
                ),
                segments: const [
                  ButtonSegment(
                    value: CommunityViewMode.admin,
                    icon:
                        Icon(Icons.admin_panel_settings_outlined, size: 16),
                    label: Text('Admin'),
                  ),
                  ButtonSegment(
                    value: CommunityViewMode.passengerBooking,
                    icon: Icon(Icons.book_online_outlined, size: 16),
                    label: Text('Booking'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(width: 8),
          IconButton(
            onPressed: onSwitchMode,
            icon: const Icon(Icons.swap_horiz_rounded),
            tooltip: 'Switch Mode',
            style: IconButton.styleFrom(
              foregroundColor: AppColors.brandGray,
            ),
          ),
        ],
      ),
    );
  }
}
