public class WeatherData
{
    public string City { get; }
    public int TemperatureCelsius { get; }
    public string Country { get; }

    public WeatherData(string city, int temperatureCelsius, string country)
    {
        City = city;
        TemperatureCelsius = temperatureCelsius;
        Country = country;
    }
}
