using System.Globalization;
using System.Text.Json;

public class WeatherClient
{
    private readonly HttpClient http;

    public WeatherClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<WeatherData> GetWeatherAsync(string city, CancellationToken token = default)
    {
        // Кодируем название, чтобы пробелы и другие символы не ломали адрес.
        string url = "https://wttr.in/" + Uri.EscapeDataString(city) + "?format=j1";
        string json = await http.GetStringAsync(url, token);
        using JsonDocument document = JsonDocument.Parse(json);

        try
        {
            JsonElement root = document.RootElement;
            string? temperature = root.GetProperty("current_condition")[0]
                .GetProperty("temp_C").GetString();
            string? country = root.GetProperty("nearest_area")[0]
                .GetProperty("country")[0].GetProperty("value").GetString();

            if (!int.TryParse(temperature, NumberStyles.Integer, CultureInfo.InvariantCulture, out int celsius)
                || string.IsNullOrWhiteSpace(country))
                throw new InvalidDataException("API не вернул температуру или страну.");

            // В модели сохраняем название из файла, страну и температуру — из API.
            return new WeatherData(city, celsius, country.Trim());
        }
        catch (Exception error) when (error is KeyNotFoundException
            or IndexOutOfRangeException or InvalidOperationException)
        {
            throw new InvalidDataException("В ответе API отсутствуют ожидаемые поля.", error);
        }
    }
}
