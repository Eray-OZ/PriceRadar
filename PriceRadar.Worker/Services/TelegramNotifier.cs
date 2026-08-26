using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace PriceRadar.Worker.Services;

public class TelegramNotifier
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay =
        TimeSpan.FromSeconds(2);

    private readonly HttpClient _client;
    private readonly string _telegramBotToken;
    private readonly string _chatId;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(
        HttpClient client,
        IConfiguration configuration,
        ILogger<TelegramNotifier> logger)
    {
        _client = client;
        _logger = logger;
        _telegramBotToken = GetRequiredSetting(
            configuration,
            "TelegramBotToken");
        _chatId = GetRequiredSetting(configuration, "ChatId");
    }

    public async Task<bool> SendPriceChangeNotificationAsync(string message)
    {
        var requestUrl = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage";

        var payload = new
        {
            chat_id = _chatId,
            text = message
        };

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using HttpResponseMessage response =
                    await _client.PostAsJsonAsync(requestUrl, payload);

                string responseBody =
                    await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode >= 500
                        && attempt < MaxAttempts)
                    {
                        _logger.LogWarning(
                            "Telegram returned HTTP status {StatusCode}. Retrying notification ({Attempt}/{MaxAttempts}).",
                            (int)response.StatusCode,
                            attempt,
                            MaxAttempts);

                        await Task.Delay(RetryDelay);
                        continue;
                    }

                    _logger.LogError(
                        "Telegram notification failed with HTTP status {StatusCode}.",
                        (int)response.StatusCode);

                    return false;
                }

                using JsonDocument responseJson =
                    JsonDocument.Parse(responseBody);

                bool telegramAccepted = responseJson.RootElement
                    .TryGetProperty("ok", out JsonElement ok)
                    && ok.ValueKind == JsonValueKind.True;

                if (!telegramAccepted)
                {
                    string description = responseJson.RootElement
                        .TryGetProperty("description", out JsonElement descriptionElement)
                        ? descriptionElement.GetString() ?? "No description returned."
                        : "No description returned.";

                    _logger.LogError(
                        "Telegram rejected the notification: {Description}",
                        description);

                    return false;
                }

                _logger.LogInformation(
                    "Telegram notification sent successfully to the configured chat.");

                return true;
            }
            catch (HttpRequestException ex)
            {
                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Telegram HTTP request failed. Retrying notification ({Attempt}/{MaxAttempts}).",
                        attempt,
                        MaxAttempts);

                    await Task.Delay(RetryDelay);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Telegram notification could not be sent because the HTTP request failed.");

                return false;
            }
            catch (TaskCanceledException ex)
            {
                if (attempt < MaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Telegram request timed out. Retrying notification ({Attempt}/{MaxAttempts}).",
                        attempt,
                        MaxAttempts);

                    await Task.Delay(RetryDelay);
                    continue;
                }

                _logger.LogError(
                    ex,
                    "Telegram notification timed out.");

                return false;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Telegram returned an unreadable response.");

                return false;
            }
        }

        return false;
    }

    private static string GetRequiredSetting(
        IConfiguration configuration,
        string key)
    {
        string? value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{key} configuration is missing or empty.",
                key);
        }

        return value;
    }
}
