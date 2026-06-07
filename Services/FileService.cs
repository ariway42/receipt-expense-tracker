namespace ReceiptExpenseTracker.Services
{
    public interface IFileService
    {
        Task<string?> SaveReceiptImageAsync(IFormFile file, int transactionId);
        void DeleteReceiptImage(string? filePath);
        bool IsValidImage(IFormFile file);
    }

    public class FileService : IFileService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileService> _logger;

        public FileService(IConfiguration configuration, ILogger<FileService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> SaveReceiptImageAsync(IFormFile file, int transactionId)
        {
            try
            {
                var uploadsFolder = _configuration["UploadSettings:ReceiptPath"] ?? "uploads/receipts";

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var extension = Path.GetExtension(file.FileName);
                var fileName = $"receipt_{transactionId}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("Saved receipt image: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving receipt image");
                return null;
            }
        }

        public void DeleteReceiptImage(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted receipt image: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting receipt image: {FilePath}", filePath);
            }
        }

        public bool IsValidImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return false;

            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
            return allowedContentTypes.Contains(file.ContentType.ToLowerInvariant());
        }
    }
}
