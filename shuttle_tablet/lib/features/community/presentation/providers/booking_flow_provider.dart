import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/di/injection_container.dart';
import '../../domain/entities/calendar_day.dart';
import '../../domain/entities/community_booking.dart';
import '../../domain/repositories/i_community_repository.dart';
import '../../domain/usecases/book_seat_usecase.dart';

class BookingFlowState {
  final TripDestination? destination;
  final TripDirection? direction;
  final TripType? tripType;
  final CalendarDay? selectedDay;
  final String? fullName;
  final String? phone;
  final String? email;
  final CommunityBooking? result;
  final bool isLoading;
  final String? error;

  const BookingFlowState({
    this.destination,
    this.direction,
    this.tripType,
    this.selectedDay,
    this.fullName,
    this.phone,
    this.email,
    this.result,
    this.isLoading = false,
    this.error,
  });

  BookingFlowState copyWith({
    TripDestination? destination,
    TripDirection? direction,
    TripType? tripType,
    CalendarDay? selectedDay,
    String? fullName,
    String? phone,
    String? email,
    CommunityBooking? result,
    bool? isLoading,
    String? error,
    bool clearResult = false,
    bool clearError = false,
  }) {
    return BookingFlowState(
      destination: destination ?? this.destination,
      direction: direction ?? this.direction,
      tripType: tripType ?? this.tripType,
      selectedDay: selectedDay ?? this.selectedDay,
      fullName: fullName ?? this.fullName,
      phone: phone ?? this.phone,
      email: email ?? this.email,
      result: clearResult ? null : (result ?? this.result),
      isLoading: isLoading ?? this.isLoading,
      error: clearError ? null : (error ?? this.error),
    );
  }
}

final bookingFlowProvider =
    NotifierProvider<BookingFlowNotifier, BookingFlowState>(
        BookingFlowNotifier.new);

class BookingFlowNotifier extends Notifier<BookingFlowState> {
  @override
  BookingFlowState build() => const BookingFlowState();

  void setDestination(TripDestination destination) {
    state = state.copyWith(destination: destination);
  }

  void setRoute(TripDirection direction, TripType tripType) {
    state = state.copyWith(direction: direction, tripType: tripType);
  }

  void setDate(CalendarDay day) {
    state = state.copyWith(selectedDay: day);
  }

  void setPassengerDetails(String fullName, String phone, String email) {
    state = state.copyWith(
        fullName: fullName, phone: phone, email: email, clearError: true);
  }

  Future<bool> submitBooking() async {
    final s = state;
    if (s.destination == null ||
        s.direction == null ||
        s.tripType == null ||
        s.selectedDay == null ||
        s.fullName == null ||
        s.phone == null ||
        s.email == null) {
      return false;
    }

    state = state.copyWith(isLoading: true, clearError: true);

    final directionStr = s.direction == TripDirection.outbound ? 'Outbound' : 'Inbound';
    final tripTypeStr = s.tripType == TripType.returnTrip ? 'Return' : 'OneWay';
    final date = s.selectedDay!.date;

    final result = await sl<BookSeatUseCase>()(BookSeatParams(
      date: date,
      direction: directionStr,
      tripType: tripTypeStr,
      destination: s.destination!.apiValue,
      fullName: s.fullName!,
      phone: s.phone!,
      email: s.email!,
    ));

    return result.fold(
      (failure) {
        state = state.copyWith(isLoading: false, error: failure.message);
        return false;
      },
      (booking) {
        state = state.copyWith(isLoading: false, result: booking);
        return true;
      },
    );
  }

  void reset() {
    state = const BookingFlowState();
  }
}
