# 👋 E-Commerce Platform

A modern, feature-rich e-commerce platform built with ASP.NET Core MVC for a modest fashion scarves business. This platform provides a seamless shopping experience for guests and robust management tools for admins, including soft deletes, advanced filtering, email notifications, and secure authentication.

## ✨ Features

### 🛍️ Guest (Customer) Features
- **Guest Checkout**: No registration required to place orders—simply provide details during checkout.
- **Product Browsing**: View new arrivals on the homepage, browse all products, and access detailed product pages with color options.
- **Shopping Cart**: Persistent cart with session management. Add items from home or product lists (defaults to primary color; specify colors on details page). Apply promo codes to see savings from product discounts or codes.
- **Product Reviews**: Leave star ratings and comments on products. Approved reviews display on the homepage and product pages.
- **Multiple Payment Methods**: 
  - Cash on Delivery (COD): Requires 50% deposit; instructions shown on checkout.
  - Credit Card: Secure Paymob integration with card details entry.
  - Wallet: Enter wallet details for processing.
  - After payment, redirect to confirmation page with order details and automatic email notification.
- **Order Tracking**: Real-time status updates via email (e.g., shipped, delivered).
- **Responsive Design**: Mobile-friendly interface using Bootstrap 5.

### 👑 Admin Features
- **Dashboard Analytics**: Overview of pending/delivered orders, total revenue, and key statistics for quick insights.
- **Order Management**: 
  - View all orders with advanced filters (by order ID/name, date, status like pending/processing/shipped/delivered, payment method like cash/credit/wallet, customer name/email).
  - Update status, cancel orders (with reason; sends automatic cancellation email), or process refunds.
  - Status changes (e.g., to shipped/delivered) trigger email notifications to customers.
- **Product Management**: Full CRUD operations for products, including images and colors. Soft delete products with related orders (hides from guests, moves to deleted list; restore anytime with full details).
- **Category Management**: CRUD for categories. If a category has products, archiving soft-deletes all related products (hides from guests; not hard-deleted). Categories with products can be soft-deleted to a hidden "deleted" section.
- **Discount Management**: 
  - Product-specific discounts.
  - Promo codes with activation dates, percentage/fixed amounts, and expiration.
- **Shipping Management**: Configure cities with costs; toggle active/inactive status.
- **Homepage Media Management**: Upload images/videos for the homepage. Add customizable buttons with titles and URLs for calls-to-action.
- **Review Moderation**: View reviews with reviewer name/email. Accept (displays publicly), reject, or delete.
- **Admin User Management**: Multi-admin support with role-based access via ASP.NET Core Identity.
- **Authentication & Security**: Secure login/logout, password change, and forgot password (email link sent for reset). Uses ASP.NET Identity for hashed passwords, token-based resets, and role enforcement.

### 💳 Payment Integration
- **Paymob Integration**: Handles credit card and wallet payments with secure iframe processing.
- **Cash on Delivery**: Integrated with deposit requirement and status tracking.
- **Automatic Updates**: Real-time payment confirmation and HMAC verification for secure callbacks.
- **Post-Payment Flow**: On success, redirect to order confirmation and send email.

### 📧 Email Notifications
- Order confirmations and details.
- Shipping/status updates (e.g., shipped, delivered).
- Order cancellations with reason.
- Review approvals/rejections.
- Password reset links and confirmations.
- Powered by SMTP (Gmail supported) with configurable settings.

## 🚀 Tech Stack
- **Framework**: ASP.NET Core MVC 6.0+
- **Database**: SQL Server / PostgreSQL (via Entity Framework Core)
- **ORM**: Entity Framework Core with migrations for schema management
- **Authentication**: ASP.NET Core Identity (roles, secure hashing, email-based password resets)
- **Payment Gateway**: Paymob (API for cards/wallets, HMAC security)
- **Email**: SMTP service with async sending
- **Caching**: Distributed Memory Cache for sessions/carts
- **Frontend**: Bootstrap 5, jQuery for dynamic elements (e.g., cart updates, filters)
- **Other**: Soft deletes via flags in models; AJAX for real-time features like promo application

## 📋 Prerequisites
- .NET 6.0 SDK or later
- SQL Server (or PostgreSQL)
- SMTP server credentials (e.g., Gmail app password for emails)
- Paymob account (API key, integration IDs for payments)

## 🛠️ Installation
1. **Clone the Repository**:
   ```bash
   git clone https://github.com/yourusername/labella-scarves.git
   cd labella-scarves
   ```

2. **Configure Database**:
   Update `appsettings.json`:
   - For SQL Server:
     ```json
     {
       "ConnectionStrings": {
         "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LaBellaScarves;Trusted_Connection=True;MultipleActiveResultSets=true"
       }
     }
     ```
   - For PostgreSQL:
     ```json
     {
       "ConnectionStrings": {
         "DefaultConnection": "Host=localhost;Database=LaBellaScarves;Username=postgres;Password=yourpassword"
       }
     }
     ```

3. **Configure Email Settings**:
   Create `appsettings.Development.json` (do not commit):
   ```json
   {
     "SmtpSettings": {
       "Host": "smtp.gmail.com",
       "Port": 587,
       "Username": "your-email@gmail.com",
       "Password": "your-app-password",
       "FromEmail": "noreply@yourdomain.com",
       "FromName": "LaBella Scarves",
       "EnableSsl": true
     }
   }
   ```

4. **Configure Paymob** (for payments):
   Add to `appsettings.Development.json`:
   ```json
   {
     "Paymob": {
       "ApiKey": "your-paymob-api-key",
       "IntegrationIdCard": "your-card-integration-id",
       "IntegrationIdWallet": "your-wallet-integration-id",
       "IframeIdCard": "your-iframe-id",
       "HMACSecret": "your-hmac-secret"
     }
   }
   ```

5. **Apply Database Migrations**:
   ```bash
   dotnet ef database update
   ```

6. **Run the Application**:
   ```bash
   dotnet run
   ```
   Access at `https://localhost:5001`. Default admin login is set via DbInitializer (local only; create production admins via registration or DB).

## 🏗️ Project Structure
```
LaBellaScarves/
├── Core/                  # Business logic (DTOs, Services like EmailService, Interfaces)
├── Domain/                # Models (Entities with soft-delete flags, e.g., IsDeleted)
├── Infrastructure/        # Data access (DbContext, Repositories, Migrations)
└── Web/                   # MVC layer
    ├── Areas/             # Admin area controllers (e.g., OrdersController with filters)
    ├── Controllers/       # Guest controllers (Home, Cart, Checkout)
    ├── Views/             # Razor views (e.g., Admin/Dashboard.cshtml with charts)
    ├── wwwroot/           # Static files (images, JS for AJAX filters/cart)
    └── Program.cs         # Startup with Identity, EF, Services registration
```

## 👥 User Roles
- **Guests**: Browse, cart, checkout, reviews (no login needed).
- **Admins**: Full access via `/Admin` area. Roles enforced by ASP.NET Identity. Initial admins seeded locally; production setup via registration or direct DB insert.

## 🔒 Security Considerations
- **ASP.NET Identity**: Handles authentication, authorization, password hashing, and email-confirmed resets.
- **Soft Deletes**: Prevents data loss for products/categories with relations.
- **Best Practices**: 
  - Never commit credentials (use User Secrets or env vars).
  - Remove hardcoded seeds in production.
  - Enable HTTPS, secure cookies, and rate limiting.
  - HMAC for Paymob callbacks; validate all inputs to prevent injections.

## 🧪 Testing
Run unit/integration tests:
```bash
dotnet test
```
(Covers services like order filtering, email sending, payment mocks.)

## 🚢 Deployment
### Production Checklist
- ✅ Remove test credentials; use env vars (e.g., `ConnectionStrings__DefaultConnection`).
- ✅ Enable HTTPS and CORS.
- ✅ Configure logging (Serilog) and backups.
- ✅ Set up CDN for media (images/videos).
- ✅ Monitor with Application Insights.
- ✅ Seed initial admin securely.

Sample env vars:
```bash
export ConnectionStrings__DefaultConnection="Server=prod-server;Database=LaBellaScarves;User Id=sa;Password=secure-pass;"
export SmtpSettings__Username="noreply@yourdomain.com"
export Paymob__ApiKey="prod-api-key"
```


## 📝 License
Proprietary and confidential. Unauthorized use prohibited.

## 📧 Contact
For support or inquiries, connect on LinkedIn: [Maiar Hussein](https://www.linkedin.com/in/maiar-hussein/)