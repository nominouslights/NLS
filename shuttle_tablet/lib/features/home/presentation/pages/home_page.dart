import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/routing/route_names.dart';
import '../../../../core/theme/app_colors.dart';
import '../../../clients/presentation/pages/clients_list_page.dart';
import '../../../drivers/presentation/pages/drivers_list_page.dart';
import '../../../locations/presentation/pages/saved_locations_page.dart';
import '../../../trips/presentation/pages/trips_page.dart';
import '../../../vehicles/presentation/pages/vehicles_list_page.dart';
import '../widgets/home_dashboard.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  int _selectedIndex = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: const _HomeAppBar(),
      body: IndexedStack(
        index: _selectedIndex,
        children: [
          HomeDashboard(
            onNavigateTo: (i) => setState(() => _selectedIndex = i),
          ),
          const ClientsListPage(),
          const TripsPage(),
          const DriversListPage(),
          const VehiclesListPage(),
          const SavedLocationsPage(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _selectedIndex,
        onDestinationSelected: (i) => setState(() => _selectedIndex = i),
        backgroundColor: Colors.white,
        indicatorColor: AppColors.primary.withValues(alpha: 0.12),
        surfaceTintColor: Colors.transparent,
        shadowColor: Colors.black.withValues(alpha: 0.08),
        elevation: 8,
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home_rounded, color: AppColors.primary),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.business_outlined),
            selectedIcon: Icon(Icons.business_rounded, color: AppColors.primary),
            label: 'Clients',
          ),
          NavigationDestination(
            icon: Icon(Icons.route_outlined),
            selectedIcon: Icon(Icons.route_rounded, color: AppColors.primary),
            label: 'Dispatch',
          ),
          NavigationDestination(
            icon: Icon(Icons.people_outline_rounded),
            selectedIcon: Icon(Icons.people_rounded, color: AppColors.primary),
            label: 'Drivers',
          ),
          NavigationDestination(
            icon: Icon(Icons.directions_bus_outlined),
            selectedIcon: Icon(Icons.directions_bus_rounded,
                color: AppColors.primary),
            label: 'Fleet',
          ),
          NavigationDestination(
            icon: Icon(Icons.bookmark_border_rounded),
            selectedIcon: Icon(Icons.bookmark_rounded, color: AppColors.primary),
            label: 'Locations',
          ),
        ],
      ),
    );
  }
}

// ─── Custom App Bar ───────────────────────────────────────────────────────────

class _HomeAppBar extends StatelessWidget implements PreferredSizeWidget {
  const _HomeAppBar();

  @override
  Size get preferredSize => const Size.fromHeight(64);

  @override
  Widget build(BuildContext context) {
    return Container(
      height: preferredSize.height,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: Color(0xFFE5E7EB))),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 24),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.primary,
              borderRadius: BorderRadius.circular(10),
              boxShadow: [
                BoxShadow(
                  color: AppColors.primary.withValues(alpha: 0.3),
                  blurRadius: 20,
                ),
              ],
            ),
            child: const Icon(
              Icons.local_shipping_rounded,
              color: Colors.white,
              size: 20,
            ),
          ),
          const SizedBox(width: 12),
          const Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Northern Link',
                style: TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF111827),
                  height: 1.2,
                ),
              ),
              Text(
                'SHUTTLE MANAGEMENT',
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.w500,
                  color: AppColors.brandGray,
                  letterSpacing: 0.8,
                  height: 1.2,
                ),
              ),
            ],
          ),
          const Spacer(),
          IconButton(
            onPressed: () => context.go(RouteNames.modeSelection),
            icon: const Icon(Icons.swap_horiz_rounded),
            tooltip: 'Switch Mode',
            style: IconButton.styleFrom(
              foregroundColor: AppColors.brandGray,
            ),
          ),
          const SizedBox(width: 4),
          Stack(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: const Color(0xFFF9FAFB),
                  shape: BoxShape.circle,
                  border: Border.all(color: const Color(0xFFE5E7EB)),
                ),
                child: const Icon(
                  Icons.notifications_none_rounded,
                  color: AppColors.brandGray,
                  size: 20,
                ),
              ),
              Positioned(
                top: 8,
                right: 8,
                child: Container(
                  width: 8,
                  height: 8,
                  decoration: const BoxDecoration(
                    color: AppColors.danger,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(width: 12),
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: const BoxDecoration(
                  color: AppColors.primary,
                  shape: BoxShape.circle,
                ),
                child: const Center(
                  child: Text(
                    'AD',
                    style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              const Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Admin User',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF111827),
                      height: 1.2,
                    ),
                  ),
                  Text(
                    'System Owner',
                    style: TextStyle(
                      fontSize: 12,
                      color: AppColors.brandGray,
                      height: 1.2,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }
}

