class ServerException implements Exception {
  final String message;
  final int? statusCode;
  const ServerException({this.message = 'Server error', this.statusCode});
}

class NetworkException implements Exception {
  const NetworkException();
}

class UnauthorizedException implements Exception {
  const UnauthorizedException();
}

class CacheException implements Exception {
  const CacheException();
}

class ConflictException implements Exception {
  final String message;
  const ConflictException([this.message = 'A conflict occurred.']);
}
