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

            return $"Berhasil terdaftar! Selamat datang {user.FirstName ?? user.Email}!\n\nKamu bisa mulai catat pengeluaran dengan format:\n*beli [item] [toko] [jumlah] [harga]*\n\nContoh:\nbeli nasi warung pak indro 2 20000";
        }

        private async Task<string> HandleTransaction(string phone, string message)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user == null)
                return "Kamu belum terdaftar. Ketik /daftar email@kamu.com untuk daftar.";

            var parsed = ParseTransactionMessage(message);
            if (parsed == null)
                return "Format tidak dikenali.\n\nFormat yang benar:\n*beli [item] [toko] [jumlah] [harga]*\n\nContoh:\nbeli nasi warung pak indro 2 20000";

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

            return $"✅ Transaksi tersimpan!\n📍 {parsed.StoreName}\n🛒 {parsed.ItemName} x{parsed.Quantity}\n💰 Rp{parsed.Amount:N0}";
        }

        private ParsedTransaction? ParseTransactionMessage(string message)
        {
            // Format: beli [item] [toko] [jumlah] [harga]
            // Contoh: beli nasi warung pak indro 2 20000
            var pattern = @"^beli\s+(.+?)\s+(.+?)\s+(\d+)\s+([\d.,]+)$";
            var match = System.Text.RegularExpressions.Regex.Match(
                message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success) return null;

            var itemName = match.Groups[1].Value.Trim();
            var storeName = match.Groups[2].Value.Trim();
            var qty = int.TryParse(match.Groups[3].Value, out var q) ? q : 1;
            var amountStr = match.Groups[4].Value.Replace(".", "").Replace(",", "");

            if (!decimal.TryParse(amountStr, out var amount)) return null;

            return new ParsedTransaction
            {
                ItemName = itemName,
                StoreName = storeName,
                Quantity = qty,
                Amount = amount * qty
            };
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