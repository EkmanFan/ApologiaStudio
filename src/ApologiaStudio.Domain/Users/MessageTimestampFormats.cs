namespace ApologiaStudio.Domain.Users;

public static class MessageTimestampFormats
{
    public const string DayMonthYear = "dd/MM/yyyy";
    public const string MonthDayYear = "MM/dd/yyyy";
    public const string IsoYearMonthDay = "yyyy-MM-dd";

    public const string TwentyFourHourWithSeconds = "HH:mm:ss";
    public const string TwentyFourHour = "HH:mm";
    public const string TwelveHourWithSeconds = "hh:mm:ss tt";
    public const string TwelveHour = "hh:mm tt";

    public static bool IsSupportedDateFormat(string? format)
    {
        return format is
            DayMonthYear or
            MonthDayYear or
            IsoYearMonthDay;
    }

    public static bool IsSupportedTimeFormat(string? format)
    {
        return format is
            TwentyFourHourWithSeconds or
            TwentyFourHour or
            TwelveHourWithSeconds or
            TwelveHour;
    }

    public static void EnsureSupportedDateFormat(
        string format,
        string parameterName)
    {
        if (!IsSupportedDateFormat(format))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                format,
                "Unsupported message date format.");
        }
    }

    public static void EnsureSupportedTimeFormat(
        string format,
        string parameterName)
    {
        if (!IsSupportedTimeFormat(format))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                format,
                "Unsupported message time format.");
        }
    }
}
