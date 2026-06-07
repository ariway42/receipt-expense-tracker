# Receipt Expense Tracker

A modern ASP.NET Core 8 MVC application for tracking expenses through receipt uploads with OCR processing.

## Features

- **Dashboard**: Overview of spending with interactive charts
- **Receipt Upload**: Drag-and-drop or camera capture with simulated OCR processing
- **Transaction Management**: Full CRUD operations with search and filtering
- **Reports**: Interactive charts by daily/weekly/monthly/yearly periods
- **Excel Export**: Export transactions and reports to Excel format
- **Responsive Design**: Mobile-first Bootstrap 5 UI

## Technology Stack

- **Backend**: ASP.NET Core 8 MVC
- **Database**: SQL Server with Entity Framework Core
- **Frontend**: Razor Views, HTML5, CSS3, Bootstrap 5, JavaScript/jQuery
- **Charts**: Chart.js
- **Excel Export**: DocumentFormat.OpenXml
- **OCR**: Tesseract (simulated for demo)

## Project Structure

```
ReceiptExpenseTracker/
├── Controllers/
│   ├── HomeController.cs
│   ├── TransactionsController.cs
│   ├── UploadController.cs
│   └── ReportsController.cs
├── Models/
│   ├── Transaction.cs
│   └── ErrorViewModel.cs
├── Views/
│   ├── Home/
│   ├── Transactions/
│   ├── Upload/
│   ├── Reports/
│   └── Shared/
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Repositories/
├── Services/
│   ├── TransactionService.cs
│   ├── OcrService.cs
│   └── FileService.cs
├── wwwroot/
│   ├── css/
│   └── js/
├── Database/
│   ├── CreateDatabase.sql
│   └── SeedData.sql
├── Program.cs
├── appsettings.json
└── web.config
```

## Database Setup

1. **Create the database**:
   ```sql
   -- Run in SQL Server Management Studio
   :r Database\CreateDatabase.sql
   ```

2. **Seed sample data** (optional):
   ```sql
   :r Database\SeedData.sql
   ```

3. **Update connection string** in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER;Database=ReceiptExpenseTracker;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
     }
   }
   ```

## Development

### Prerequisites
- .NET 8 SDK
- SQL Server 2019+
- Visual Studio 2022 or VS Code

### Run locally
```bash
# Restore packages
dotnet restore

# Run migrations (if using EF migrations)
dotnet ef migrations add InitialCreate
dotnet ef database update

# Run the application
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

## Deployment to IIS

### Prerequisites
- Windows Server with IIS
- .NET 8 Hosting Bundle installed
- SQL Server accessible

### Steps

1. **Publish the application**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Create IIS Application Pool**:
   - Open IIS Manager
   - Create new App Pool: `ReceiptExpenseTracker`
   - Set .NET CLR Version: `No Managed Code`
   - Managed Pipeline Mode: `Integrated`

3. **Create IIS Website**:
   - Right-click "Sites" > "Add Website"
   - Site name: `ReceiptExpenseTracker`
   - Physical path: Point to the `publish` folder
   - App Pool: Select `ReceiptExpenseTracker`
   - Binding: Configure hostname and port

4. **Configure permissions**:
   - Grant IIS_IUSRS read/write permissions to the publish folder
   - Create `uploads/receipts` folder and grant write permissions

5. **Update appsettings.json** in the published folder:
   - Update connection string for production SQL Server
   - Set `ASPNETCORE_ENVIRONMENT` to `Production`

6. **Test the site**:
   - Browse to the configured URL
   - Verify database connectivity

### web.config

The included `web.config` is configured for IIS deployment with:
- AspNetCoreModuleV2 handler
- stdout logging enabled
- Static content MIME types

## Configuration

### Upload Settings
Configure in `appsettings.json`:
```json
{
  "UploadSettings": {
    "ReceiptPath": "uploads/receipts",
    "MaxFileSizeMB": 10
  }
}
```

### Supported File Types
- JPG/JPEG
- PNG

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/` | GET | Dashboard |
| `/Transactions` | GET | List transactions |
| `/Transactions/Details/{id}` | GET | View transaction |
| `/Transactions/Edit/{id}` | GET/POST | Edit transaction |
| `/Transactions/Delete/{id}` | POST | Delete transaction |
| `/Upload` | GET | Upload form |
| `/Upload/ProcessReceipt` | POST | Process uploaded receipt |
| `/Upload/Save` | POST | Save transaction |
| `/Reports` | GET | Reports dashboard |
| `/Reports/Export` | GET | Export to Excel |

## License

MIT License
