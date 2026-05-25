import 'dart:async';

enum AuthEvent { forceLogout }

class AuthEventBus {
  final _controller = StreamController<AuthEvent>.broadcast();
  Stream<AuthEvent> get stream => _controller.stream;
  void forceLogout() => _controller.add(AuthEvent.forceLogout);
  void dispose() => _controller.close();
}
