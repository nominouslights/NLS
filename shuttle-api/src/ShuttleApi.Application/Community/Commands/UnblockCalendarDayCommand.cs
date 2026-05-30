using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Commands;

public sealed record UnblockCalendarDayCommand(DateOnly Date) : ICommand;
