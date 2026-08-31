using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Application.Calendar;

/// <summary>Errors specific to the calendar queries.</summary>
public static class CalendarErrors
{
    public static readonly Error InvalidMonth = Error.Validation(
        "Booking.Calendar.InvalidMonth", "The calendar month must be 1–12 in a year between 2000 and 2100.");
}
