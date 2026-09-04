using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InvoiceAI.Application;
using InvoiceAI.Application.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceAI.Infrastructure.Gemini;

public class GeminiInvoiceExtractor : IInvoiceExtractor
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiInvoiceExtractor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiInvoiceExtractor(
        HttpClient http,
        IOptions<GeminiOptions> options,
        ILogger<GeminiInvoiceExtractor> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private const string PromptText = """
        You are an expert accounting assistant extracting data from a supplier invoice.
        The invoice may be written in French, Arabic or English, and may come from Tunisia
        or elsewhere. Read every page of the document.

        Rules:
        - Return dates in ISO format yyyy-MM-dd. Interpret ambiguous numeric dates as
          day/month/year, which is the convention used in France and Tunisia.
        - Return all amounts as plain numbers with a dot as the decimal separator.
          Remove thousands separators, currency symbols and spaces. 1 250,500 becomes 1250.5
        - The supplier is the party issuing the invoice and being paid.
          The customer is the party who must pay.
        - tax_id is the fiscal identifier (matricule fiscal, VAT number, ICE, SIRET).
        - vat_rate is a percentage, so 19% must be returned as 19, not 0.19
        - If a field is not visible in the document, return null for it.
          Never invent, guess or infer a value that is not printed on the invoice.
        - If the document is not an invoice at all, return null for every field.
        """;

    private const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "invoice_number": { "type": ["string", "null"] },
            "issue_date":     { "type": ["string", "null"], "format": "date" },
            "due_date":       { "type": ["string", "null"], "format": "date" },
            "supplier": {
              "type": ["object", "null"],
              "properties": {
                "name":    { "type": ["string", "null"] },
                "address": { "type": ["string", "null"] },
                "tax_id":  { "type": ["string", "null"] }
              }
            },
            "customer": {
              "type": ["object", "null"],
              "properties": {
                "name":    { "type": ["string", "null"] },
                "address": { "type": ["string", "null"] },
                "tax_id":  { "type": ["string", "null"] }
              }
            },
            "line_items": {
              "type": ["array", "null"],
              "items": {
                "type": "object",
                "properties": {
                  "description": { "type": ["string", "null"] },
                  "quantity":    { "type": ["number", "null"] },
                  "unit_price":  { "type": ["number", "null"] },
                  "vat_rate":    { "type": ["number", "null"] },
                  "line_total":  { "type": ["number", "null"] }
                }
              }
            },
            "subtotal":   { "type": ["number", "null"] },
            "vat_amount": { "type": ["number", "null"] },
            "total":      { "type": ["number", "null"] },
            "currency":   { "type": ["string", "null"] }
          },
          "required": ["invoice_number", "total", "currency"]
        }
        """;

    public async Task<ExtractionResultDto> ExtractAsync(
        Stream document,
        string mimeType,
        CancellationToken ct)
    {
        string base64;
        using (var buffer = new MemoryStream())
        {
            await document.CopyToAsync(buffer, ct);
            base64 = Convert.ToBase64String(buffer.ToArray());
        }

        var payload = BuildPayload(base64, mimeType);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
                request.Headers.Add("x-goog-api-key", _options.ApiKey);
                request.Headers.Add("Api-Revision", _options.ApiRevision);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    return ParseResponse(body);
                }

                if (IsTransient(response.StatusCode) && attempt <= _options.MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogWarning(
                        "Gemini returned {Status}. Attempt {Attempt} of {Max}. Retrying in {Delay}s.",
                        (int)response.StatusCode, attempt, _options.MaxRetries, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                    continue;
                }

                _logger.LogError("Gemini call failed with {Status}: {Body}",
                    (int)response.StatusCode, Truncate(body));

                return new ExtractionResultDto(false, null, null,
                    $"Gemini returned HTTP {(int)response.StatusCode}.");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested
                                                && attempt <= _options.MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("Gemini call timed out. Attempt {Attempt}. Retrying in {Delay}s.",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (HttpRequestException ex) when (attempt <= _options.MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Network error calling Gemini. Attempt {Attempt}. Retrying in {Delay}s.",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    private string BuildPayload(string base64, string mimeType)
    {
        var blockType = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? "image"
            : "document";

        var payload = new JsonObject
        {
            ["model"] = _options.Model,
            ["input"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = blockType,
                    ["data"] = base64,
                    ["mime_type"] = mimeType
                },
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = PromptText
                }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "text",
                ["mime_type"] = "application/json",
                ["schema"] = JsonNode.Parse(SchemaJson)
            }
        };

        return payload.ToJsonString();
    }

    private ExtractionResultDto ParseResponse(string body)
    {
        string? text = null;

        try
        {
            using var envelope = JsonDocument.Parse(body);

            if (!envelope.RootElement.TryGetProperty("steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array)
            {
                return new ExtractionResultDto(false, null, null,
                    "Gemini response contained no steps array.");
            }

            foreach (var step in steps.EnumerateArray())
            {
                if (!step.TryGetProperty("type", out var stepType)
                    || stepType.GetString() != "model_output")
                {
                    continue;
                }

                if (!step.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var blockType)
                        && blockType.GetString() == "text"
                        && block.TryGetProperty("text", out var textNode))
                    {
                        text = textNode.GetString();
                        break;
                    }
                }

                if (text is not null)
                {
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            return new ExtractionResultDto(false, null, null,
                $"Gemini response was not valid JSON: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ExtractionResultDto(false, null, null,
                "Gemini response contained no model_output text block.");
        }

        try
        {
            var invoice = JsonSerializer.Deserialize<ExtractedInvoiceDto>(text, JsonOptions);

            return invoice is null
                ? new ExtractionResultDto(false, null, text, "Extraction JSON deserialized to null.")
                : new ExtractionResultDto(true, invoice, text, null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Model returned malformed extraction JSON: {Text}", Truncate(text));
            return new ExtractionResultDto(false, null, text,
                $"Model returned invalid JSON: {ex.Message}");
        }
    }

    private static bool IsTransient(HttpStatusCode status) => status is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.InternalServerError or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}
