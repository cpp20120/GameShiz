using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Client;

public sealed record BotFrameworkClientOptions(
    Uri BaseAddress,
    Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider = null,
    JsonSerializerOptions? JsonSerializerOptions = null);

public sealed record BotFrameworkTenantContext(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId? PlayerId = null);

public sealed record BotFrameworkProblemDetails(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    string? Code,
    string? CorrelationId,
    int? RetryAfterSeconds);

public sealed class BotFrameworkApiException(
    BotFrameworkProblemDetails problem,
    HttpStatusCode statusCode)
    : HttpRequestException(problem.Detail ?? problem.Title, null, statusCode)
{
    public BotFrameworkProblemDetails Problem { get; } = problem;
}

/// <summary>
/// Small transport client used by generated module clients. The generated
/// OpenAPI surface can compose this class while keeping auth, tenant context,
/// idempotency, correlation and RFC 7807 handling identical for every module.
/// </summary>
public sealed class BotFrameworkClient(HttpClient httpClient, BotFrameworkClientOptions options)
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TResponse> SendAsync<TRequest, TResponse>(
        HttpMethod method,
        string module,
        string operation,
        BotFrameworkTenantContext tenantContext,
        TRequest? body = default,
        string? idempotencyKey = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var changesState = method != HttpMethod.Get
            && method != HttpMethod.Head
            && method != HttpMethod.Options;
        if (changesState && string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("State-changing requests require an Idempotency-Key.", nameof(idempotencyKey));

        using var request = new HttpRequestMessage(
            method,
            new Uri(
                options.BaseAddress,
                $"api/v1/tenants/{Uri.EscapeDataString(tenantContext.TenantId.Value)}/scopes/{Uri.EscapeDataString(tenantContext.ScopeId.Value)}/{module.Trim('/')}/{operation.Trim('/')}")
                .AbsoluteUri);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: options.JsonSerializerOptions ?? DefaultJsonOptions);

        if (options.AccessTokenProvider is not null)
        {
            var token = await options.AccessTokenProvider(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var requestIdentifier = RequestId.New().Value;
        request.Headers.TryAddWithoutValidation("X-Request-ID", requestIdentifier);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId ?? requestIdentifier);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        return (await response.Content.ReadFromJsonAsync<TResponse>(options.JsonSerializerOptions ?? DefaultJsonOptions, cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("The BotFramework response body was empty.");
    }

    private static async Task<BotFrameworkApiException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        BotFrameworkProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<BotFrameworkProblemDetails>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Preserve a typed error even when a proxy returns a non-problem body.
        }

        problem ??= new BotFrameworkProblemDetails(
            null,
            response.ReasonPhrase,
            (int)response.StatusCode,
            null,
            null,
            "http_error",
            response.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.FirstOrDefault() : null,
            response.Headers.RetryAfter?.Delta is { } delta ? (int)Math.Ceiling(delta.TotalSeconds) : null);
        return new BotFrameworkApiException(problem, response.StatusCode);
    }
}
