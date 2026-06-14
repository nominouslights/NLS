import 'package:freezed_annotation/freezed_annotation.dart';
import '../../domain/entities/trip_passenger_email_log.dart';

part 'trip_passenger_email_log_model.freezed.dart';
part 'trip_passenger_email_log_model.g.dart';

@freezed
class TripPassengerEmailLogModel with _$TripPassengerEmailLogModel {
  const factory TripPassengerEmailLogModel({
    required String id,
    required String tripPassengerId,
    required String recipientEmail,
    required String direction,
    required DateTime sentAt,
    required bool isTest,
  }) = _TripPassengerEmailLogModel;

  factory TripPassengerEmailLogModel.fromJson(Map<String, dynamic> json) =>
      _$TripPassengerEmailLogModelFromJson(json);
}

extension TripPassengerEmailLogModelX on TripPassengerEmailLogModel {
  TripPassengerEmailLog toEntity() => TripPassengerEmailLog(
        id: id,
        tripPassengerId: tripPassengerId,
        recipientEmail: recipientEmail,
        direction: direction,
        sentAt: sentAt,
        isTest: isTest as bool,
      );
}
