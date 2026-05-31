import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class SecureStorageService {
  final FlutterSecureStorage _storage;

  SecureStorageService(this._storage);

  Future<void> saveAccessToken(String token) =>
      _storage.write(key: 'access_token', value: token);

  Future<String?> getAccessToken() => _storage.read(key: 'access_token');

  Future<void> saveRefreshToken(String token) =>
      _storage.write(key: 'refresh_token', value: token);

  Future<String?> getRefreshToken() => _storage.read(key: 'refresh_token');

  Future<void> saveRole(String role) =>
      _storage.write(key: 'user_role', value: role);

  Future<String?> getRole() => _storage.read(key: 'user_role');

  Future<void> saveMustChangePassword(bool value) =>
      _storage.write(key: 'must_change_password', value: value.toString());

  Future<bool> getMustChangePassword() async {
    final value = await _storage.read(key: 'must_change_password');
    return value == 'true';
  }

  Future<void> clearAll() => _storage.deleteAll();
}
