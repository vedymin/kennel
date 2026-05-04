namespace Kennel.Tests;

public class GoogleCalendarMappingTests
{
    [Fact]
    public void Map_AllDayEvent_PreservesCheckoutDateAndGoogleMetadata()
    {
        var mapper = new GoogleCalendarEventMapper(TimeZoneInfo.Utc);
        var item = mapper.Map(new GoogleCalendarEvent(
            Id: "event-123",
            Summary: "Figa",
            Start: new GoogleCalendarEventDate(Date: "2026-06-10", DateTime: null),
            End: new GoogleCalendarEventDate(Date: "2026-06-13", DateTime: null)));

        Assert.Equal("google:event-123", item.Id);
        Assert.Equal("google", item.Source);
        Assert.Equal("Figa", item.DogName);
        Assert.Equal("2026-06-10", item.StartDate);
        Assert.Equal("2026-06-13", item.EndDate);
        Assert.Null(item.CreatedAt);
        Assert.False(item.CanDelete);
    }

    [Fact]
    public void Map_DateTimeEvent_UsesApplicationTimezoneDate()
    {
        var warsaw = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        var mapper = new GoogleCalendarEventMapper(warsaw);

        var item = mapper.Map(new GoogleCalendarEvent(
            Id: "night-event",
            Summary: "Reks",
            Start: new GoogleCalendarEventDate(Date: null, DateTime: "2026-06-10T22:30:00Z"),
            End: new GoogleCalendarEventDate(Date: null, DateTime: "2026-06-12T08:00:00Z")));

        Assert.Equal("2026-06-11", item.StartDate);
        Assert.Equal("2026-06-12", item.EndDate);
    }

    [Fact]
    public void Map_WhitespaceTitle_UsesFallbackDogName()
    {
        var mapper = new GoogleCalendarEventMapper(TimeZoneInfo.Utc);

        var item = mapper.Map(new GoogleCalendarEvent(
            Id: "untitled",
            Summary: "  ",
            Start: new GoogleCalendarEventDate(Date: "2026-06-10", DateTime: null),
            End: new GoogleCalendarEventDate(Date: "2026-06-11", DateTime: null)));

        Assert.Equal("(bez imienia)", item.DogName);
    }
}
