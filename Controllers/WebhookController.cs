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

        public WebhookController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<WebhookController> logger)
        {
            _userManager = userManager;
            _context = context;
            _config = config;
            _logger = logger;
        }

        [HttpPost("message")]
        public async Task<IActionResult> HandleMessage([FromBody] FonntteWebhookPayload payload)
        {
            var phone = payload.Sender?.Replace("+", "").Replace("-", "").Replace(" ", "");
            var message = payload.Message?.Trim() ?? "";

            if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(message))
                return Ok(new { reply = "" });

            // /daftar email@example.com
            if (message.StartsWith("/daftar "))
            {
                var email = message.Substring(8).Trim();
                return Ok(new { reply = await HandleRegister(phone, email) });
            }

            // /verifikasi 123456
            if (message.StartsWith("/verifikasi "))
            {
                var code = message.Substring(12).Trim();
                return Ok(new { reply = await HandleVerify(phone, code) });
            }

            // Transaksi biasa
            return Ok(new { reply = await HandleTransaction(phone, message) });
        }

        private async Task<string> HandleRegister(string phone, string email)
        {
            // Cek email terdaftar di web
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return "Email tidak terdaftar. Daftar dulu di web ya!";

            // Cek nomor WA sudah terdaftar
            var existingPhone = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);
            if (existingPhone != null)
                return "Nomor kamu sudah terdaftar!";

            // Cek email sudah dipakai nomor WA lain
            if (!string.IsNullOrEmpty(user.PhoneNumberWA))
                return "Email ini sudah terdaftar di nomor lain!";

            // Hapus OTP lama jika ada
            var oldOtps = _context.WaOtps
                .Where(o => o.PhoneNumber == phone && !o.IsUsed);
            _context.WaOtps.RemoveRange(oldOtps);

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();
            _context.WaOtps.Add(new WaOtp
            {
                PhoneNumber = phone,
                Email = email,
                OtpCode = otp,
                ExpiredAt = DateTime.UtcNow.AddMinutes(10)
            });
            await _context.SaveChangesAsync();

            // Kirim OTP via email
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

            return $"Berhasil terdaftar! Selamat datang {user.FirstName ?? user.Email}! Kamu bisa mulai catat pengeluaran. Contoh: beli makan di warung pak indro 20000";
        }

        private async Task<string> HandleTransaction(string phone, string message)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.PhoneNumberWA == phone);

            if (user == null)
                return "Kamu belum terdaftar. Ketik /daftar email@kamu.com untuk daftar.";

            // Parse pesan sederhana: "beli X di Y Rp Z" atau "X di Y Z"
            var parsed = ParseTransactionMessage(message);
            if (parsed == null)
                return "Format tidak dikenali. Contoh: beli makan di warung pak indro 20000";

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
                        Price = parsed.Amount,
                        Quantity = 1
                    }
                }
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            return $"✅ Transaksi tersimpan!\n📍 {parsed.StoreName}\n🛒 {parsed.ItemName}\n💰 Rp{parsed.Amount:N0}";
        }

        private ParsedTransaction? ParseTransactionMessage(string message)
        {
            // Pattern: "beli X di Y 20000" atau "X di Y 20000"
            var pattern = @"(?:beli\s+)?(.+?)\s+di\s+(.+?)\s+([\d.,]+)$";
            var match = System.Text.RegularExpressions.Regex.Match(
                message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success) return null;

            var itemName = match.Groups[1].Value.Trim();
            var storeName = match.Groups[2].Value.Trim();
            var amountStr = match.Groups[3].Value.Replace(".", "").Replace(",", "");

            if (!decimal.TryParse(amountStr, out var amount)) return null;

            return new ParsedTransaction
            {
                ItemName = itemName,
                StoreName = storeName,
                Amount = amount
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
    }
}