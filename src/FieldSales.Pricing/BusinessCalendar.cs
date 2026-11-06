namespace FieldSales.Pricing;

/// <summary>Which calendar day it is for pricing (MI-53, WI-059 D1): the date in Ireland, wherever the device is and whatever time
/// zone it is set to. The tablet and the server share it, so a future-dated price starts at the same midnight on both.</summary>
public static class BusinessCalendar
{
    /// <summary>The business time zone: Europe/Dublin, Irish Standard Time in summer and GMT in winter.</summary>
    public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");

    /// <summary>The date in Ireland at <paramref name="now"/>.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZone).DateTime);

    /// <summary>The day a downloaded snapshot prices on (WI-059 D2): <paramref name="today"/>, but never a day before the snapshot was
    /// built, so a tablet clock set back can't quote prices older than the download.</summary>
    public static DateOnly PriceDay(DateOnly today, DateOnly builtOn) => today < builtOn ? builtOn : today;
}
