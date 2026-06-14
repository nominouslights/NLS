// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'trip_passenger_email_log_model.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

_$TripPassengerEmailLogModelImpl _$$TripPassengerEmailLogModelImplFromJson(
  Map<String, dynamic> json,
) => _$TripPassengerEmailLogModelImpl(
  id: json['id'] as String,
  tripPassengerId: json['tripPassengerId'] as String,
  recipientEmail: json['recipientEmail'] as String,
  direction: json['direction'] as String,
  sentAt: DateTime.parse(json['sentAt'] as String),
  isTest: json['isTest'] as bool,
);

Map<String, dynamic> _$$TripPassengerEmailLogModelImplToJson(
  _$TripPassengerEmailLogModelImpl instance,
) => <String, dynamic>{
  'id': instance.id,
  'tripPassengerId': instance.tripPassengerId,
  'recipientEmail': instance.recipientEmail,
  'direction': instance.direction,
  'sentAt': instance.sentAt.toIso8601String(),
  'isTest': instance.isTest,
};
