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

            if (message.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                reply = await HandleStart(phone);
            }
            else if (message.Equals("/help", StringComparison.OrdinalIgnoreCase))
            {
                reply = await HandleHelp(phone);
            }
            else if (message.StartsWith("/laporan", StringComparison.OrdinalIgnoreCase))
            {
                reply = await HandleLaporan(phone, message);
            }
            else if (message.StartsWith("/daftar "))
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

        private async Task<string> HandleStart(string phone)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user != null)
            {
                var nama = user.FirstName ?? user.Email ?? "kamu";
                return $"👋 Selamat datang kembali, *{nama}*!\n\n" +
                       $"🛒 *Catat pengeluaran*\n" +
                       $"• `beli di toko pak ahmad rokok surya 2 100000`\n" +
                       $"• `beli di warteg bu siti nasi ayam 15000`\n" +
                       $"• `beli di shell bensin 50000`\n\n" +
                       $"📦 Multi-item: pisahkan dengan *dan*\n" +
                       $"🧾 Multi-transaksi: pisahkan dengan *,*\n\n" +
                       $"📖 Ketik `/help` untuk panduan lengkap.\n\n" +
                       $"👉 Detail laporan, edit & hapus transaksi tersedia di *web Finansia*";
            }
            else
            {
                return $"👋 Halo! Selamat datang di *Finansia Bot*!\n\n" +
                       $"Bot ini membantu kamu mencatat pengeluaran langsung lewat WhatsApp.\n\n" +
                       $"Untuk mulai, daftarkan nomor kamu dulu:\n\n" +
                       $"📧 Ketik:\n" +
                       $"`/daftar emailkamu@gmail.com`\n\n" +
                       $"_Pastikan email yang kamu daftarkan sudah terdaftar di web Finansia ya!_";
            }
        }

        private Task<string> HandleHelp(string phone)
        {
            var msg = "📖 *Panduan Finansia Bot*\n\n" +

            "🛒 *Catat pengeluaran*\n" +
            "Format:\n" +
            "`beli di [toko] [barang] [jumlah] [harga]`\n\n" +

            "_Contoh:_\n" +
            "• `beli di toko pak ahmad rokok surya 2 100000`\n" +
            "• `beli di warteg bu siti nasi ayam 15000`\n" +
            "• `beli di shell bensin 50000`\n\n" +

            "📦 *Multi-item* (pakai kata *dan*)\n" +
            "• `beli di toko pak ahmad rokok surya 2 30000 dan sabun 2 40000`\n\n" +

            "🧾 *Multi-transaksi* (pisahkan dengan koma)\n" +
            "• `beli di toko pak ahmad rokok surya 2 30000, beli di toko pak ahdi ayam goreng 20000`\n\n" +

            "💡 *Tips*\n" +
            "Harga yang ditulis adalah *total yang dibayar*, bukan harga satuan.\n" +
            "Contoh: `kopi 2 20000` berarti 2 kopi dengan total Rp20.000.\n\n" +

            "⚙️ *Perintah*\n\n" +

            "• `/laporan hari ini`\n" +
            "  Lihat total pengeluaran hari ini\n\n" +

            "• `/laporan bulan ini`\n" +
            "  Lihat total pengeluaran bulan ini\n\n" +

            "• `/laporan tahun ini`\n" +
            "  Lihat total pengeluaran tahun ini\n\n" +

            "• `/daftar email@gmail.com`\n" +
            "  Hubungkan nomor WhatsApp dengan akun Finansia\n\n" +

            "📊 Untuk melihat laporan lengkap, mengubah, atau menghapus transaksi:\n" +
            "👉 Buka *web Finansia*\n\n" +

            "🙏 Terima kasih sudah menggunakan *Finansia Bot*. Semoga pencatatan keuangan jadi lebih mudah!";

            return Task.FromResult(msg);
        }

        private async Task<string> HandleLaporan(string phone, string message)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user == null)
                return "Kamu belum terdaftar. Ketik /daftar email@kamu.com untuk daftar.";

            var now = DateTime.UtcNow;
            DateTime startDate;
            string periodLabel;

            var cmd = message.Trim().ToLower();

            if (cmd == "/laporan hari ini" || cmd == "/laporan harian")
            {
                startDate = now.Date;
                periodLabel = $"Hari Ini ({now:dd MMMM yyyy})";
            }
            else if (cmd == "/laporan bulan ini" || cmd == "/laporan bulanan" || cmd == "/laporan")
            {
                startDate = new DateTime(now.Year, now.Month, 1);
                periodLabel = $"{now:MMMM yyyy}";
            }
            else if (cmd == "/laporan tahun ini" || cmd == "/laporan tahunan")
            {
                startDate = new DateTime(now.Year, 1, 1);
                periodLabel = $"Tahun {now.Year}";
            }
            else
            {
                return "Format tidak dikenali.\n\nGunakan:\n• `/laporan hari ini`\n• `/laporan bulan ini`\n• `/laporan tahun ini`";
            }

            var transactions = await _context.Transactions
                .Where(t => t.UserId == user.Id && t.TransactionDate >= startDate)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            if (!transactions.Any())
                return $"📊 Belum ada transaksi untuk periode *{periodLabel}*.";

            var total = transactions.Sum(t => t.TotalAmount);
            var jumlah = transactions.Count;

            var topToko = transactions
                .GroupBy(t => t.StoreName)
                .OrderByDescending(g => g.Sum(t => t.TotalAmount))
                .Take(3)
                .Select(g => $"• {g.Key}: Rp{g.Sum(t => t.TotalAmount):N0}")
                .ToList();

            var recent = transactions
                .Take(3)
                .Select(t => $"• {t.TransactionDate:dd/MM} {t.StoreName}: Rp{t.TotalAmount:N0}")
                .ToList();

            return $"📊 *Laporan {periodLabel}*\n\n" +
                   $"💰 Total: *Rp{total:N0}*\n" +
                   $"🧾 Transaksi: *{jumlah}x*\n\n" +
                   $"🏪 *Toko terbanyak:*\n{string.Join("\n", topToko)}\n\n" +
                   $"🕐 *Transaksi terakhir:*\n{string.Join("\n", recent)}\n\n" +
                   $"👉 Detail lengkap di *web Finansia*";
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
                return "❌ Email tidak ditemukan.\n\n" +
           "Silakan daftar akun terlebih dahulu di web Finansia, lalu ulangi:\n\n" +
           "`/daftar email@gmail.com`";

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

            return $"Kode OTP telah dikirim ke {email}. Ketik:\n`/verifikasi 123456`\nuntuk konfirmasi. Kode berlaku 10 menit.";
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

            return $"✅ Berhasil terdaftar! Selamat datang *{user.FirstName ?? user.Email}*!\n\n" +
                   $"Kamu bisa mulai catat pengeluaran:\n\n" +
                   $"_Contoh:_\n" +
                   $"• `beli di toko pak ahmad rokok surya 2 100000`\n" +
                   $"• `beli di warteg bu siti nasi ayam 1 15000`\n\n" +
                   $"Ketik `/help` untuk panduan lengkap.\n\n" +
                   $"_Untuk edit atau hapus transaksi, buka web Finansia._";
        }

        private async Task<string> HandleTransaction(string phone, string message)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user == null)
                return "Kamu belum terdaftar. Ketik /daftar email@kamu.com untuk daftar.";

            var segments = message.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var replies = new List<string>();
            foreach (var segment in segments)
            {
                var parsed = await ParseTransactionWithAI(segment);
                if (parsed == null)
                {
                    replies.Add(
                                 $"⚠️ Tidak bisa memahami transaksi:\n" +
                                 $"`{segment}`\n\n" +
                                 $"Contoh:\n" +
                                 $"`beli di warteg nasi ayam 15000`"
                             );
                    continue;
                }

                var totalAmount = parsed.Items.Sum(i => i.Amount);
                var transaction = new Transaction
                {
                    StoreName = parsed.StoreName,
                    TotalAmount = totalAmount,
                    TransactionDate = DateTime.Today,
                    UserId = user.Id,
                    CreatedDate = DateTime.UtcNow,
                    TransactionItems = parsed.Items.Select(i => new TransactionItem
                    {
                        ItemName = i.ItemName,
                        Price = i.Amount / i.Quantity,
                        Quantity = i.Quantity
                    }).ToList()
                };

                _context.Transactions.Add(transaction);

                var itemLines = string.Join("\n", parsed.Items.Select(i => $"  🛒 {i.ItemName} x{i.Quantity} — Rp{i.Amount:N0}"));
                replies.Add($"✅ *{parsed.StoreName}*\n{itemLines}\n  💰 Total: Rp{totalAmount:N0}");
            }

            await _context.SaveChangesAsync();

            return string.Join("\n\n", replies) + "\n\n_Untuk edit atau hapus, buka web Finansia._";
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
                  "  \"store_name\": \"nama toko/tempat\",\n" +
                  "  \"items\": [\n" +
                  "    { \"item_name\": \"nama barang\", \"quantity\": 1, \"total_amount\": 20000 }\n" +
                  "  ]\n" +
                  "}\n\n" +
                  "Aturan:\n" +
                  "- Satu transaksi bisa punya BANYAK item, dipisah kata 'dan'\n" +
                  "- Harga yang disebutkan user = TOTAL per item yang dibayar, bukan harga satuan\n" +
                  "- quantity default 1 jika tidak disebutkan\n" +
                  "- Jika harga tidak disebutkan sama sekali, return is_transaction: false\n" +
                  "- Jika bukan transaksi pembelian, return is_transaction: false\n" +
                  "- store_name = 'Tidak diketahui' jika tidak disebutkan\n\n" +
                  $"Pesan: {message}";

                var requestBody = new
                {
                    model = "meta-llama/llama-4-scout-17b-16e-instruct",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0,
                    max_tokens = 512
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

                var storeName = root.TryGetProperty("store_name", out var s) ? s.GetString() ?? "Tidak diketahui" : "Tidak diketahui";

                if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return null;

                var items = new List<TransactionItemParsed>();
                foreach (var el in itemsEl.EnumerateArray())
                {
                    var itemName = el.TryGetProperty("item_name", out var i) ? i.GetString() ?? "Item" : "Item";
                    var qty = el.TryGetProperty("quantity", out var q) && q.ValueKind == System.Text.Json.JsonValueKind.Number ? (int)q.GetDecimal() : 1;
                    var total = el.TryGetProperty("total_amount", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.Number ? t.GetDecimal() : 0;
                    if (total > 0)
                        items.Add(new TransactionItemParsed { ItemName = itemName, Quantity = qty, Amount = total });
                }

                if (!items.Any()) return null;

                return new ParsedTransaction
                {
                    StoreName = storeName,
                    Items = items
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
        public List<TransactionItemParsed> Items { get; set; } = new();
    }

    public class TransactionItemParsed
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Quantity { get; set; } = 1;
    }
}