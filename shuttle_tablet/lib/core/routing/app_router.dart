import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../features/auth/presentation/pages/login_page.dart';
import '../../features/auth/presentation/pages/pending_approval_page.dart';
import '../../features/auth/presentation/pages/register_page.dart';
import '../../features/auth/presentation/providers/auth_provider.dart';
import '../../features/home/presentation/pages/home_page.dart';
import '../../features/users/presentation/pages/pending_users_page.dart';
import 'route_names.dart';

final routerProvider = Provider<GoRouter>((ref) {
  final notifier = _RouterNotifier(ref);
  ref.onDispose(notifier.dispose);
  return GoRouter(
    initialLocation: RouteNames.login,
    refreshListenable: notifier,
    redirect: notifier.redirect,
    routes: [
      GoRoute(
        path: RouteNames.home,
        builder: (_, __) => const HomePage(),
      ),
      GoRoute(
        path: RouteNames.login,
        builder: (_, __) => const LoginPage(),
      ),
      GoRoute(
        path: RouteNames.register,
        builder: (_, __) => const RegisterPage(),
      ),
      GoRoute(
        path: RouteNames.pendingApproval,
        builder: (_, __) => const PendingApprovalPage(),
      ),
      GoRoute(
        path: RouteNames.pendingUsers,
        builder: (_, __) => const PendingUsersPage(),
      ),
      GoRoute(
        path: RouteNames.trips,
        builder: (_, __) => const _PlaceholderPage(title: 'Trips'),
      ),
      GoRoute(
        path: RouteNames.drivers,
        builder: (_, __) => const _PlaceholderPage(title: 'Drivers'),
      ),
      GoRoute(
        path: RouteNames.passengers,
        builder: (_, __) => const _PlaceholderPage(title: 'Passengers'),
      ),
      GoRoute(
        path: RouteNames.vehicles,
        builder: (_, __) => const _PlaceholderPage(title: 'Vehicles'),
      ),
    ],
  );
});

class _RouterNotifier extends ChangeNotifier {
  final Ref _ref;

  _RouterNotifier(this._ref) {
    _ref.listen(authProvider, (_, __) => notifyListeners());
    _ref.listen(userRoleProvider, (_, __) => notifyListeners());
  }

  String? redirect(BuildContext context, GoRouterState state) {
    final auth = _ref.read(authProvider);
    final roleAsync = _ref.read(userRoleProvider);

    if (auth.isLoading || roleAsync.isLoading) return null;

    final isAuthenticated = auth.valueOrNull != null;
    final role = roleAsync.valueOrNull;
    final location = state.matchedLocation;

    // Public routes — always accessible
    final publicRoutes = {RouteNames.login, RouteNames.register, RouteNames.pendingApproval};
    final isPublic = publicRoutes.contains(location);

    if (!isAuthenticated) {
      return isPublic ? null : RouteNames.login;
    }

    // Authenticated: redirect away from public routes
    if (isPublic) {
      return role == 'Admin' ? RouteNames.pendingUsers : RouteNames.home;
    }

    // Protect admin route from non-admins
    if (location == RouteNames.pendingUsers && role != 'Admin') {
      return RouteNames.home;
    }

    return null;
  }

}

class _PlaceholderPage extends StatelessWidget {
  final String title;
  const _PlaceholderPage({required this.title});

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: Text(title)),
        body: Center(child: Text('$title — coming soon')),
      );
}
