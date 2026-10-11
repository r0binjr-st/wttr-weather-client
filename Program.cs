using System.Globalization;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

string path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "Cities.txt");
string[] lines;
try
{
    lines = await File.ReadAllLinesAsync(path);
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Не удалось прочитать файл городов: " + error.Message);
    return 1;
}

// Пустые строки пропускаем. Moscow и moscow считаем одним городом.
string[] cities = lines.Select(line => line.Trim())
    .Where(city => city.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (cities.Length == 0)
{
    Console.Error.WriteLine("Файл городов пуст.");
    return 1;
}

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherByCountry/1.0");
WeatherClient client = new(http);
List<WeatherData> results = new();
int errors = 0;
bool cancelled = false;

Console.WriteLine($"Уникальных городов: {cities.Length}");
Console.WriteLine("Текущая погода:");

// Последовательные запросы не создают лишнюю нагрузку на бесплатный API.
foreach (string city in cities)
{
    try
    {
        WeatherData weather = await client.GetWeatherAsync(city, cancellation.Token);
        results.Add(weather);
        Console.WriteLine($"{weather.City}, {weather.Country} {Temperature(weather.TemperatureCelsius)} °C");
    }
    catch (OperationCanceledException)
    {
        if (cancellation.IsCancellationRequested)
        {
            cancelled = true;
            Console.Error.WriteLine("Запросы отменены.");
            break;
        }
        errors++;
        Console.Error.WriteLine($"{city}: API не ответил за 30 секунд.");
    }
    catch (Exception error) when (error is HttpRequestException or JsonException or InvalidDataException)
    {
        errors++;
        Console.Error.WriteLine($"{city}: {error.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Получено результатов: {results.Count} из {cities.Length}");
if (results.Count > 0)
{
    Console.WriteLine("Сводка по странам" + (errors > 0 || cancelled ? " (неполная):" : ":"));
    foreach (var country in results.GroupBy(weather => weather.Country, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
    {
        double average = country.Average(weather => weather.TemperatureCelsius);
        int minimum = country.Min(weather => weather.TemperatureCelsius);
        int maximum = country.Max(weather => weather.TemperatureCelsius);
        string averageText = average.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
        int count = country.Count();
        string cityWord = count == 1 ? "city" : "cities";

        Console.WriteLine($"{country.Key} — {count} {cityWord}, avg: {averageText} °C, "
            + $"min: {Temperature(minimum)} °C, max: {Temperature(maximum)} °C");
    }
}

return cancelled ? 130 : errors > 0 ? 1 : 0;

static string Temperature(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
