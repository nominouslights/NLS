import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

enum AppMode { charter, community }

enum CommunityViewMode { admin, passengerBooking }

final appModeProvider =
    AsyncNotifierProvider<AppModeNotifier, AppMode>(AppModeNotifier.new);

final communityViewModeProvider =
    StateProvider<CommunityViewMode>((ref) => CommunityViewMode.admin);

class AppModeNotifier extends AsyncNotifier<AppMode> {
  static const _key = 'app_mode';

  @override
  Future<AppMode> build() async {
    final prefs = await SharedPreferences.getInstance();
    final saved = prefs.getString(_key);
    return saved == 'community' ? AppMode.community : AppMode.charter;
  }

  Future<void> setMode(AppMode mode) async {
    state = const AsyncLoading();
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(
        _key, mode == AppMode.community ? 'community' : 'charter');
    state = AsyncData(mode);
  }
}
