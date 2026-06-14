// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint
// ignore_for_file: unused_element, deprecated_member_use, deprecated_member_use_from_same_package, use_function_type_syntax_for_parameters, unnecessary_const, avoid_init_to_null, invalid_override_different_default_values_named, prefer_expression_function_bodies, annotate_overrides, invalid_annotation_target, unnecessary_question_mark

part of 'trip_passenger_email_log_model.dart';

// **************************************************************************
// FreezedGenerator
// **************************************************************************

T _$identity<T>(T value) => value;

final _privateConstructorUsedError = UnsupportedError(
  'It seems like you constructed your class using `MyClass._()`. This constructor is only meant to be used by freezed and you are not supposed to need it nor use it.\nPlease check the documentation here for more information: https://github.com/rrousselGit/freezed#adding-getters-and-methods-to-our-models',
);

TripPassengerEmailLogModel _$TripPassengerEmailLogModelFromJson(
  Map<String, dynamic> json,
) {
  return _TripPassengerEmailLogModel.fromJson(json);
}

/// @nodoc
mixin _$TripPassengerEmailLogModel {
  String get id => throw _privateConstructorUsedError;
  String get tripPassengerId => throw _privateConstructorUsedError;
  String get recipientEmail => throw _privateConstructorUsedError;
  String get direction => throw _privateConstructorUsedError;
  DateTime get sentAt => throw _privateConstructorUsedError;
  bool get isTest => throw _privateConstructorUsedError;

  /// Serializes this TripPassengerEmailLogModel to a JSON map.
  Map<String, dynamic> toJson() => throw _privateConstructorUsedError;

  /// Create a copy of TripPassengerEmailLogModel
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  $TripPassengerEmailLogModelCopyWith<TripPassengerEmailLogModel>
  get copyWith => throw _privateConstructorUsedError;
}

/// @nodoc
abstract class $TripPassengerEmailLogModelCopyWith<$Res> {
  factory $TripPassengerEmailLogModelCopyWith(
    TripPassengerEmailLogModel value,
    $Res Function(TripPassengerEmailLogModel) then,
  ) =
      _$TripPassengerEmailLogModelCopyWithImpl<
        $Res,
        TripPassengerEmailLogModel
      >;
  @useResult
  $Res call({
    String id,
    String tripPassengerId,
    String recipientEmail,
    String direction,
    DateTime sentAt,
    bool isTest,
  });
}

/// @nodoc
class _$TripPassengerEmailLogModelCopyWithImpl<
  $Res,
  $Val extends TripPassengerEmailLogModel
>
    implements $TripPassengerEmailLogModelCopyWith<$Res> {
  _$TripPassengerEmailLogModelCopyWithImpl(this._value, this._then);

  // ignore: unused_field
  final $Val _value;
  // ignore: unused_field
  final $Res Function($Val) _then;

  /// Create a copy of TripPassengerEmailLogModel
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? tripPassengerId = null,
    Object? recipientEmail = null,
    Object? direction = null,
    Object? sentAt = null,
    Object? isTest = null,
  }) {
    return _then(
      _value.copyWith(
            id:
                null == id
                    ? _value.id
                    : id // ignore: cast_nullable_to_non_nullable
                        as String,
            tripPassengerId:
                null == tripPassengerId
                    ? _value.tripPassengerId
                    : tripPassengerId // ignore: cast_nullable_to_non_nullable
                        as String,
            recipientEmail:
                null == recipientEmail
                    ? _value.recipientEmail
                    : recipientEmail // ignore: cast_nullable_to_non_nullable
                        as String,
            direction:
                null == direction
                    ? _value.direction
                    : direction // ignore: cast_nullable_to_non_nullable
                        as String,
            sentAt:
                null == sentAt
                    ? _value.sentAt
                    : sentAt // ignore: cast_nullable_to_non_nullable
                        as DateTime,
            isTest:
                null == isTest
                    ? _value.isTest
                    : isTest // ignore: cast_nullable_to_non_nullable
                        as bool,
          )
          as $Val,
    );
  }
}

/// @nodoc
abstract class _$$TripPassengerEmailLogModelImplCopyWith<$Res>
    implements $TripPassengerEmailLogModelCopyWith<$Res> {
  factory _$$TripPassengerEmailLogModelImplCopyWith(
    _$TripPassengerEmailLogModelImpl value,
    $Res Function(_$TripPassengerEmailLogModelImpl) then,
  ) = __$$TripPassengerEmailLogModelImplCopyWithImpl<$Res>;
  @override
  @useResult
  $Res call({
    String id,
    String tripPassengerId,
    String recipientEmail,
    String direction,
    DateTime sentAt,
    bool isTest,
  });
}

/// @nodoc
class __$$TripPassengerEmailLogModelImplCopyWithImpl<$Res>
    extends
        _$TripPassengerEmailLogModelCopyWithImpl<
          $Res,
          _$TripPassengerEmailLogModelImpl
        >
    implements _$$TripPassengerEmailLogModelImplCopyWith<$Res> {
  __$$TripPassengerEmailLogModelImplCopyWithImpl(
    _$TripPassengerEmailLogModelImpl _value,
    $Res Function(_$TripPassengerEmailLogModelImpl) _then,
  ) : super(_value, _then);

  /// Create a copy of TripPassengerEmailLogModel
  /// with the given fields replaced by the non-null parameter values.
  @pragma('vm:prefer-inline')
  @override
  $Res call({
    Object? id = null,
    Object? tripPassengerId = null,
    Object? recipientEmail = null,
    Object? direction = null,
    Object? sentAt = null,
    Object? isTest = null,
  }) {
    return _then(
      _$TripPassengerEmailLogModelImpl(
        id:
            null == id
                ? _value.id
                : id // ignore: cast_nullable_to_non_nullable
                    as String,
        tripPassengerId:
            null == tripPassengerId
                ? _value.tripPassengerId
                : tripPassengerId // ignore: cast_nullable_to_non_nullable
                    as String,
        recipientEmail:
            null == recipientEmail
                ? _value.recipientEmail
                : recipientEmail // ignore: cast_nullable_to_non_nullable
                    as String,
        direction:
            null == direction
                ? _value.direction
                : direction // ignore: cast_nullable_to_non_nullable
                    as String,
        sentAt:
            null == sentAt
                ? _value.sentAt
                : sentAt // ignore: cast_nullable_to_non_nullable
                    as DateTime,
        isTest:
            null == isTest
                ? _value.isTest
                : isTest // ignore: cast_nullable_to_non_nullable
                    as bool,
      ),
    );
  }
}

/// @nodoc
@JsonSerializable()
class _$TripPassengerEmailLogModelImpl implements _TripPassengerEmailLogModel {
  const _$TripPassengerEmailLogModelImpl({
    required this.id,
    required this.tripPassengerId,
    required this.recipientEmail,
    required this.direction,
    required this.sentAt,
    required this.isTest,
  });

  factory _$TripPassengerEmailLogModelImpl.fromJson(
    Map<String, dynamic> json,
  ) => _$$TripPassengerEmailLogModelImplFromJson(json);

  @override
  final String id;
  @override
  final String tripPassengerId;
  @override
  final String recipientEmail;
  @override
  final String direction;
  @override
  final DateTime sentAt;
  @override
  final bool isTest;

  @override
  String toString() {
    return 'TripPassengerEmailLogModel(id: $id, tripPassengerId: $tripPassengerId, recipientEmail: $recipientEmail, direction: $direction, sentAt: $sentAt, isTest: $isTest)';
  }

  @override
  bool operator ==(Object other) {
    return identical(this, other) ||
        (other.runtimeType == runtimeType &&
            other is _$TripPassengerEmailLogModelImpl &&
            (identical(other.id, id) || other.id == id) &&
            (identical(other.tripPassengerId, tripPassengerId) ||
                other.tripPassengerId == tripPassengerId) &&
            (identical(other.recipientEmail, recipientEmail) ||
                other.recipientEmail == recipientEmail) &&
            (identical(other.direction, direction) ||
                other.direction == direction) &&
            (identical(other.sentAt, sentAt) || other.sentAt == sentAt) &&
            (identical(other.isTest, isTest) || other.isTest == isTest));
  }

  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  int get hashCode => Object.hash(
    runtimeType,
    id,
    tripPassengerId,
    recipientEmail,
    direction,
    sentAt,
    isTest,
  );

  /// Create a copy of TripPassengerEmailLogModel
  /// with the given fields replaced by the non-null parameter values.
  @JsonKey(includeFromJson: false, includeToJson: false)
  @override
  @pragma('vm:prefer-inline')
  _$$TripPassengerEmailLogModelImplCopyWith<_$TripPassengerEmailLogModelImpl>
  get copyWith => __$$TripPassengerEmailLogModelImplCopyWithImpl<
    _$TripPassengerEmailLogModelImpl
  >(this, _$identity);

  @override
  Map<String, dynamic> toJson() {
    return _$$TripPassengerEmailLogModelImplToJson(this);
  }
}

abstract class _TripPassengerEmailLogModel
    implements TripPassengerEmailLogModel {
  const factory _TripPassengerEmailLogModel({
    required final String id,
    required final String tripPassengerId,
    required final String recipientEmail,
    required final String direction,
    required final DateTime sentAt,
    required final bool isTest,
  }) = _$TripPassengerEmailLogModelImpl;

  factory _TripPassengerEmailLogModel.fromJson(Map<String, dynamic> json) =
      _$TripPassengerEmailLogModelImpl.fromJson;

  @override
  String get id;
  @override
  String get tripPassengerId;
  @override
  String get recipientEmail;
  @override
  String get direction;
  @override
  DateTime get sentAt;
  @override
  bool get isTest;

  /// Create a copy of TripPassengerEmailLogModel
  /// with the given fields replaced by the non-null parameter values.
  @override
  @JsonKey(includeFromJson: false, includeToJson: false)
  _$$TripPassengerEmailLogModelImplCopyWith<_$TripPassengerEmailLogModelImpl>
  get copyWith => throw _privateConstructorUsedError;
}
