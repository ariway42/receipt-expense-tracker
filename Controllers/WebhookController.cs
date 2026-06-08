using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptExpenseTracker.Data;
using ReceiptExpenseTracker.Models;
using System.Net.Mail;
using System.Net;

namespace ReceiptExpenseTracker.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<WebhookController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public WebhookController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<WebhookController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _userManager = userManager;
            _context = context;
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("message")]
        public IActionResult HandleMessageGet()
        {
            return Ok("Webhook active");
        }

        [HttpPost("message")]
        public async Task<IActionResult> HandleMessage([FromBody] FonntteWebhookPayload payload)
        {
            var phone = payload.Sender?.Replace("+", "").Replace("-", "").Replace(" ", "");
            var message = payload.Message?.Trim() ?? "";

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(message))
                return Ok();

            string reply;

            if (message.StartsWith("/daftar "))
            {
                var email = message.Substring(8).Trim();
                reply = await HandleRegister(phone, email);
            }
            else if (message.StartsWith("/verifikasi "))
            {
                var code = message.Substring(12).Trim();
                reply = await HandleVerify(phone, code);
            }
            else
            {
                reply = await HandleTransaction(phone, message);
            }

            await SendReply(phone, reply);
            return Ok();
        }

        private async Task SendReply(string phone, string message)
        {
            try
            {
                var token = _config["Fonnte:Token"];
                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Add("Authorization", token);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("target", phone),
                    new KeyValuePair<string, string>("message", message),
                });

                await http.PostAsync("https://api.fonnte.com/send", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reply to {phone}", phone);
            }
        }

        private async Task<string> HandleRegister(string phone, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return "Email tidak terdaftar. Daftar dulu di web ya!";

            var existingPhone = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);
            if (existingPhone != null)
                return "Nomor kamu sudah terdaftar!";

            if (!string.IsNullOrEmpty(user.PhoneNumberWA))
                return "Email ini sudah terdaftar di nomor lain!";

            var oldOtps = _context.WaOtps
                .Where(o => o.PhoneNumber == phone && !o.IsUsed);
            _context.WaOtps.RemoveRange(oldOtps);

            var otp = new Random().Next(100000, 999999).ToString();
            _context.WaOtps.Add(new WaOtp
            {
                PhoneNumber = phone,
                Email = email,
                OtpCode = otp,
                ExpiredAt = DateTime.UtcNow.AddMinutes(10)
            });
            await _context.SaveChangesAsync();

            await SendOtpEmail(email, otp);

            return $"Kode OTP telah dikirim ke {email}. Ketik /verifikasi KODE untuk konfirmasi. Kode berlaku 10 menit.";
        }

        private async Task<string> HandleVerify(string phone, string code)
        {
            var otp = await _context.WaOtps
                .Where(o => o.PhoneNumber == phone && o.OtpCode == code && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (otp == null)
                return "Kode OTP salah atau tidak ditemukan.";

            if (otp.ExpiredAt < DateTime.UtcNow)
                return "Kode OTP sudah kadaluarsa. Ketik /daftar email kamu lagi.";

            var user = await _userManager.FindByEmailAsync(otp.Email);
            if (user == null)
                return "User tidak ditemukan.";

            user.PhoneNumberWA = phone;
            await _userManager.UpdateAsync(user);

            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            return $"Berhasil terdaftar! Selamat datang {user.FirstName ?? user.Email}!\n\nKamu bisa mulai catat pengeluaran, contoh:\nbeli rokok surya toko pak ahmad 2 100000\n\n_Untuk edit atau hapus transaksi, buka web Finansia._";
        }

        private async Task<string> HandleTransaction(string phone, string message)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user == null)
                return "Kamu belum terdaftar. Ketik /daftar email@kamu.com untuk daftar.";

            var parsed = await ParseTransactionWithAI(message);
            if (parsed == null)
                return "Tidak bisa memproses pesanmu. Pastikan menyebutkan nama barang, toko, jumlah, dan total harga.\n\nContoh:\nbeli rokok surya toko pak ahmad 2 100000";

            var transaction = new Transaction
            {
                StoreName = parsed.StoreName,
                TotalAmount = parsed.Amount,
                TransactionDate = DateTime.Today,
                UserId = user.Id,
                CreatedDate = DateTime.UtcNow,
                TransactionItems = new List<TransactionItem>
                {
                    new TransactionItem
                    {
                        ItemName = parsed.ItemName,
                        Price = parsed.Amount / parsed.Quantity,
                        Quantity = parsed.Quantity
                    }
                }
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return $"✅ Transaksi tersimpan!\n📍 {parsed.StoreName}\n🛒 {parsed.ItemName} x{parsed.Quantity}\n💰 Rp{parsed.Amount:N0}\n\n_Untuk edit atau hapus, buka web Finansia._";
        }

        private async Task<ParsedTransaction?> ParseTransactionWithAI(string message)
        {
            try
            {
                var apiKey = _config["Groq:ApiKey"];
                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var prompt = "Kamu adalah parser transaksi keuangan. Ekstrak data dari pesan berikut.\n" +
              "Kembalikan HANYA JSON valid, tanpa penjelasan, tanpa markdown.\n\n" +
              "Jika pesan BUKAN transaksi pembelian, kembalikan: {\"is_transaction\": false}\n\n" +
              "Jika pesan adalah transaksi, kembalikan:\n" +
              "{\n" +
              "  \"is_transaction\": true,\n" +
              "  \"item_name\": \"nama barang/jasa\",\n" +
              "  \"store_name\": \"nama toko/tempat\",\n" +
              "  \"quantity\": 1,\n" +
              "  \"total_amount\": 20000\n" +
              "}\n\n" +
              "Aturan:\n" +
              "- Harga yang disebutkan user = TOTAL yang dibayar, bukan harga satuan\n" +
              "- quantity default 1 jika tidak disebutkan\n" +
              "- Jika harga tidak disebutkan sama sekali, return is_transaction: false\n" +
              "- Jika bukan transaksi pembelian, return is_transaction: false\n" +
              "- store_name = Tidak diketahui jika tidak disebutkan\n\n" +
              $"Pesan: {message}";

                var requestBody = new
                {
                    model = "meta-llama/llama-4-scout-17b-16e-instruct",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0,
                    max_tokens = 256
                };

                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var response = await http.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json")
                );

                var responseBody = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                content = System.Text.RegularExpressions.Regex.Replace(content, @"```json|```", "").Trim();

                using var parsedDoc = System.Text.Json.JsonDocument.Parse(content);
                var root = parsedDoc.RootElement;

                if (root.TryGetProperty("is_transaction", out var isTx) &&
                    isTx.ValueKind == System.Text.Json.JsonValueKind.False)
                    return null;

                var itemName = root.TryGetProperty("item_name", out var i) ? i.GetString() ?? "Item" : "Item";
                var storeName = root.TryGetProperty("store_name", out var s) ? s.GetString() ?? "Tidak diketahui" : "Tidak diketahui";
                var qty = root.TryGetProperty("quantity", out var q) && q.ValueKind == System.Text.Json.JsonValueKind.Number ? (int)q.GetDecimal() : 1;
                var total = root.TryGetProperty("total_amount", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Number ? t.GetDecimal() : 0;

                if (total <= 0) return null;

                return new ParsedTransaction
                {
                    ItemName = itemName,
                    StoreName = storeName,
                    Quantity = qty,
                    Amount = total
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI parsing failed");
                return null;
            }
        }

        private async Task SendOtpEmail(string email, string otp)
        {
            try
            {
                var smtpHost = _config["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_config["Smtp:Port"] ?? "587");
                var smtpUser = _config["Smtp:Username"] ?? "";
                var smtpPass = _config["Smtp:Password"] ?? "";

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(smtpUser, smtpPass)
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(smtpUser, "Finansia"),
                    Subject = "Kode OTP WhatsApp Bot",
                    Body = $"Kode OTP kamu adalah: {otp}\n\nKode berlaku 10 menit.\nJangan berikan kode ini ke siapapun.",
                    IsBodyHtml = false
                };
                mail.To.Add(email);

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email");
            }
        }
    }

    public class FonntteWebhookPayload
    {
        public string? Sender { get; set; }
        public string? Message { get; set; }
    }

    public class ParsedTransaction
    {
        public string StoreName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Quantity { get; set; } = 1;
    }
}