using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;


namespace PriceRadar.Worker.Services;

public class TelegramNotifier
{


    private readonly HttpClient _client;
    private readonly string _telegramBotToken;
    private readonly string _chatId;

    public TelegramNotifier(HttpClient client, IConfiguration configuration)
    {

        _client = client;
        _telegramBotToken = configuration["TelegramBotToken"]
        ?? throw new ArgumentNullException("TelegramBotToken Not Found!");

        _chatId = configuration["ChatId"]
        ?? throw new ArgumentNullException("ChatId Not Found!");

    }



    public async Task SendPriceChangeNotificationAsync(string message)
    {
        var requestUrl = $"https://api.telegram.org/bot{_telegramBotToken}/sendMessage";

        var payload = new
        {
            chat_id = _chatId,
            text = message,
            parse_mode = "Markdown"
        };


        try
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync(requestUrl, payload);
            Console.WriteLine(response.IsSuccessStatusCode);
        }
        catch
        {
            Console.WriteLine("Error");
        }
    }

}
