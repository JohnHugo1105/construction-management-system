namespace ConstructOPX.Domain.Features.SiteDiary;

/// <summary>The day's weather on site, in the words people use. Stored as text.</summary>
public enum DiaryWeather
{
    Fair = 0,
    Cloudy = 1,
    Rain = 2,
    HeavyRain = 3,
    Storm = 4
}

/// <summary>A report is Posted until it is voided (entered by mistake). A voided report stays on record and frees its date.</summary>
public enum DiaryStatus
{
    Posted = 0,
    Void = 1
}
