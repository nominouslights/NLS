import 'package:equatable/equatable.dart';

class VehicleServiceRecord extends Equatable {
  final String id;
  final String vehicleId;
  final String serviceCategory;
  final String? fluidType;
  final String title;
  final String? description;
  final bool isPlanned;
  final String serviceStatus;
  final String priority;
  final DateTime? scheduledDate;
  final DateTime? startedDate;
  final DateTime? completedDate;
  final int? odometerAtService;
  final double? estimatedCostDollars;
  final double? actualCostDollars;
  final String? serviceProvider;
  final String? technicianName;
  final String? partsNotes;
  final bool isWarrantyWork;
  final DateTime? nextServiceDueDateUtc;
  final int? nextServiceDueOdometerKm;
  final DateTime createdAt;

  const VehicleServiceRecord({
    required this.id,
    required this.vehicleId,
    required this.serviceCategory,
    this.fluidType,
    required this.title,
    this.description,
    required this.isPlanned,
    required this.serviceStatus,
    required this.priority,
    this.scheduledDate,
    this.startedDate,
    this.completedDate,
    this.odometerAtService,
    this.estimatedCostDollars,
    this.actualCostDollars,
    this.serviceProvider,
    this.technicianName,
    this.partsNotes,
    required this.isWarrantyWork,
    this.nextServiceDueDateUtc,
    this.nextServiceDueOdometerKm,
    required this.createdAt,
  });

  bool get isCompleted => serviceStatus.toLowerCase() == 'completed';
  bool get isOverdue =>
      isPlanned &&
      scheduledDate != null &&
      scheduledDate!.isBefore(DateTime.now()) &&
      (serviceStatus.toLowerCase() == 'scheduled' ||
          serviceStatus.toLowerCase() == 'deferred');

  @override
  List<Object?> get props => [
        id,
        vehicleId,
        serviceCategory,
        fluidType,
        title,
        description,
        isPlanned,
        serviceStatus,
        priority,
        scheduledDate,
        startedDate,
        completedDate,
        odometerAtService,
        estimatedCostDollars,
        actualCostDollars,
        serviceProvider,
        technicianName,
        partsNotes,
        isWarrantyWork,
        nextServiceDueDateUtc,
        nextServiceDueOdometerKm,
        createdAt,
      ];
}
