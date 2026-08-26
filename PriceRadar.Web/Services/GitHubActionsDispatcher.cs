using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PriceRadar.Web.Services;

public sealed class GitHubActionsDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubActionsDispatcher> _logger;

    public GitHubActionsDispatcher(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GitHubActionsDispatcher> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> TryDispatchPriceCheckAsync(
        CancellationToken cancellationToken = default)
    {
        string? token = _configuration["GitHubActions:Token"];

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning(
                "GitHub Actions token is not configured. The pending product " +
                "will be processed by the next scheduled workflow.");
            return false;
        }

        string owner = _configuration["GitHubActions:Owner"] ?? "Eray-OZ";
        string repository =
            _configuration["GitHubActions:Repository"] ?? "PriceRadar";
        string workflow =
            _configuration["GitHubActions:Workflow"] ?? "price-check.yml";
        string gitReference =
            _configuration["GitHubActions:Ref"] ?? "main";

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"repos/{Uri.EscapeDataString(owner)}/" +
            $"{Uri.EscapeDataString(repository)}/actions/workflows/" +
            $"{Uri.EscapeDataString(workflow)}/dispatches");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("PriceRadar-Web");
        request.Content = JsonContent.Create(new { @ref = gitReference });

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                request,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException
            || ex is TaskCanceledException)
        {
            _logger.LogError(
                ex,
                "GitHub Actions dispatch request could not be sent.");
            return false;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string responseBody =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogError(
                    "GitHub Actions dispatch failed. StatusCode={StatusCode}; " +
                    "Response={Response}",
                    (int)response.StatusCode,
                    responseBody);

                return false;
            }
        }

        _logger.LogInformation(
            "GitHub Actions price-check workflow dispatched for {Owner}/{Repository}.",
            owner,
            repository);

        return true;
    }
}
