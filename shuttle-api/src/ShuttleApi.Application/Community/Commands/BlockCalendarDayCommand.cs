using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Commands;

public sealed record BlockCalendarDayCommand(DateOnly Date, string Reason)
    : ICommand<BlockCalendarDayResult>;

public sealed record BlockCalendarDayResult(int PassengersCancelled);
