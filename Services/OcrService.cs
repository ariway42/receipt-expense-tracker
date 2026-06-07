using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ReceiptExpenseTracker.Services
{
    public interface IOcrService
    {
        Task<OcrResult> ProcessReceiptAsync(string filePath);
    }

    public class OcrResult
    {
        public bool Success { get; set; }
        public string? StoreName { get; set; }
        public DateTime? TransactionDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public List<OcrItem> Items { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? RawText { get; set; }
    }

    public class OcrItem
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class OcrService : IOcrService
    {
        private readonly ILogger<OcrService> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public OcrService(
            ILogger<OcrService> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<OcrResult> ProcessReceiptAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return new OcrResult { Success = false, ErrorMessage = "File not found" };

                var apiKey = _config["Groq:ApiKey"]
                    ?? throw new InvalidOperationException("Groq:ApiKey not configured");

                // Baca image → base64
                var imageBytes = await File.ReadAllBytesAsync(filePath);
                var base64 = Convert.ToBase64String(imageBytes);
                var ext = Path.GetExtension(filePath).ToLower();
                var mediaType = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };

                // Prompt minta JSON langsung — tidak perlu parser kompleks
                var prompt = """
                                You are a receipt parser. Extract data from this receipt image.
                                Return ONLY valid JSON, no explanation, no markdown, no code block.

                                IMPORTANT: If the image is NOT a receipt (e.g. anime, selfie, random photo, food photo, etc), return exactly:
                                { "is_receipt": false }

                                If there are MULTIPLE receipts in one image, merge all items into one receipt.
                                Use the store name of the first receipt, and sum all total amounts.

                                If it IS a receipt, return a single object:
                                {
                                  "is_receipt": true,
                                  "store_name": "string",
                                  "transaction_date": "YYYY-MM-DD",
                                  "total_amount": 96000,
                                  "items": [
                                    { "name": "Item dari struk 1", "price": 35000, "quantity": 1 },
                                    { "name": "Item dari struk 2", "price": 6000, "quantity": 2 }
                                  ]
                                }

                                Rules:
                                - Always return a single JSON object, never an array
                                - If multiple receipts: combine ALL items from all receipts into one items array
                                - If multiple receipts: total_amount = sum of all receipts
                                - price = unit price, bukan subtotal
                                - Indonesian number format: 35.000 = 35000
                                - Ignore: date header, cashier, tax, subtotal, tender, change, wifi password
                                - transaction_date: null if not found
                                """;

                var requestBody = new
                {
                    model = "meta-llama/llama-4-scout-17b-16e-instruct",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:{mediaType};base64,{base64}"
                                    }
                                },
                                new
                                {
                                    type = "text",
                                    text = prompt
                                }
                            }
                        }
                    },
                    temperature = 0,
                    max_tokens = 1024
                };

                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                var json = JsonSerializer.Serialize(requestBody);
                var response = await http.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Groq response: {body}", responseBody);

                if (!response.IsSuccessStatusCode)
                    return new OcrResult { Success = false, ErrorMessage = responseBody };

                return ParseGroqResponse(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed for: {filePath}", filePath);
                return new OcrResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private OcrResult ParseGroqResponse(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);

                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                _logger.LogInformation("Groq content: {content}", content);

                content = Regex.Replace(content, @"```json|```", "").Trim();

                using var parsed = JsonDocument.Parse(content);
                var root = parsed.RootElement;

                // Fallback kalau model tetap return array: merge semua
                if (root.ValueKind == JsonValueKind.Array)
                {
                    if (root.GetArrayLength() == 0)
                        return new OcrResult { Success = false, ErrorMessage = "No receipt data found." };

                    var mergedItems = new List<OcrItem>();
                    decimal mergedTotal = 0;
                    string? mergedStore = null;

                    foreach (var receiptEl in root.EnumerateArray())
                    {
                        if (mergedStore == null && receiptEl.TryGetProperty("store_name", out var sn))
                            mergedStore = sn.GetString();

                        if (receiptEl.TryGetProperty("total_amount", out var ta) && ta.ValueKind == JsonValueKind.Number)
                            mergedTotal += ta.GetDecimal();

                        if (receiptEl.TryGetProperty("items", out var its))
                        {
                            foreach (var item in its.EnumerateArray())
                            {
                                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "UNKNOWN" : "UNKNOWN";
                                decimal price = 0;
                                if (item.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number)
                                    price = p.GetDecimal();
                                int qty = 1;
                                if (item.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number)
                                    qty = (int)Math.Round(q.GetDecimal());
                                mergedItems.Add(new OcrItem { ItemName = name, Price = price, Quantity = Math.Max(1, qty) });
                            }
                        }
                    }

                    return new OcrResult
                    {
                        Success = true,
                        StoreName = mergedStore,
                        TransactionDate = DateTime.Today,
                        TotalAmount = mergedTotal,
                        Items = mergedItems,
                        RawText = content
                    };
                }

                // Cek apakah gambar adalah struk
                if (root.TryGetProperty("is_receipt", out var isReceiptEl) &&
                    isReceiptEl.ValueKind == JsonValueKind.False)
                {
                    return new OcrResult { Success = false, ErrorMessage = "Image does not appear to be a receipt. Please upload a valid receipt image." };
                }

                var result = new OcrResult { Success = true, RawText = content };

                // Store name
                if (root.TryGetProperty("store_name", out var storeName))
                    result.StoreName = storeName.GetString();

                // Date — selalu pakai hari ini
                result.TransactionDate = DateTime.Today;

                // Total
                if (root.TryGetProperty("total_amount", out var totalEl))
                {
                    result.TotalAmount = totalEl.ValueKind == JsonValueKind.Number
                        ? totalEl.GetDecimal()
                        : decimal.TryParse(totalEl.GetString(), out var t) ? t : null;
                }

                // Items
                if (root.TryGetProperty("items", out var itemsEl))
                {
                    foreach (var item in itemsEl.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var n)
                            ? n.GetString() ?? "UNKNOWN"
                            : "UNKNOWN";

                        decimal price = 0;
                        if (item.TryGetProperty("price", out var p))
                            price = p.ValueKind == JsonValueKind.Number
                                ? p.GetDecimal()
                                : decimal.TryParse(p.GetString(), out var pv) ? pv : 0;

                        int qty = 1;
                        if (item.TryGetProperty("quantity", out var q))
                        {
                            if (q.ValueKind == JsonValueKind.Number)
                                qty = (int)Math.Round(q.GetDecimal());
                            else if (q.ValueKind == JsonValueKind.String)
                                qty = int.TryParse(q.GetString(), out var qv) ? qv : 1;
                        }

                        result.Items.Add(new OcrItem
                        {
                            ItemName = name,
                            Price = price,
                            Quantity = Math.Max(1, qty)
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Groq response");
                return new OcrResult { Success = false, ErrorMessage = "Failed to parse AI response: " + ex.Message };
            }
        }
    }
}