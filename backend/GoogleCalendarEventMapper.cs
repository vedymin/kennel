public record GoogleCalendarEvent(
    string Id,
    string? Summary,
    GoogleCalendarEventDate Start,
    GoogleCalendarEventDate End);

public record GoogleCalendarEventDate(string? Date, string? DateTime);

public class GoogleCalendarEventMapper(TimeZoneInfo applicationTimeZone)
{
    public ReservationResponse Map(GoogleCalendarEvent calendarEvent)
    {
        var startDate = MapDate(calendarEvent.Start);
        var endDate = MapDate(calendarEvent.End);
        var dogName = string.IsNullOrWhiteSpace(calendarEvent.Summary)
            ? "(bez imienia)"
            : calendarEvent.Summary.Trim();

        return new ReservationResponse(
            $"google:{calendarEvent.Id}",
            "google",
            dogName,
            startDate.ToString("yyyy-MM-dd"),
            endDate.ToString("yyyy-MM-dd"),
            null,
            false);
    }

    private DateOnly MapDate(GoogleCalendarEventDate date)
    {
        if (!string.IsNullOrWhiteSpace(date.Date))
            return DateOnly.Parse(date.Date);

        if (string.IsNullOrWhiteSpace(date.DateTime))
            throw new InvalidOperationException("Google Calendar event date must include date or dateTime.");

        var instant = DateTimeOffset.Parse(date.DateTime);
        var localTime = TimeZoneInfo.ConvertTime(instant, applicationTimeZone);
        return DateOnly.FromDateTime(localTime.DateTime);
    }
}
