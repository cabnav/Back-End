# EV Charging Station Management System

A comprehensive backend system for managing electric vehicle charging stations, built with .NET 9 and modern architectural patterns.

## 🚀 Features

### Core Functionality
- **Charging Session Management** - Start, stop, and monitor charging sessions
- **Station & Point Management** - Manage charging stations and individual charging points
- **User Management** - Multi-role user system (Drivers, Admins, Station Staff)
- **Reservation System** - Book charging slots in advance
- **Real-time Monitoring** - Live session updates via SignalR
- **Payment Integration** - Multiple payment methods (Wallet, VNPay, MoMo)
- **Notification System** - Email and real-time notifications
- **Analytics & Reporting** - Usage analytics and business intelligence

### Technical Features
- **JWT Authentication** - Secure token-based authentication
- **Role-based Authorization** - Fine-grained access control
- **Real-time Communication** - SignalR for live updates
- **Payment Gateway Integration** - VNPay and MoMo support
- **Email Services** - Automated email notifications
- **QR Code Generation** - For reservations and payments
- **Comprehensive API** - RESTful API with Swagger documentation

## 🏗️ Architecture

### Project Structure
```
EVChargingStation/
├── EVCharging.BE.API/          # Web API Layer
├── EVCharging.BE.Services/      # Business Logic Layer
├── EVCharging.BE.DAL/           # Data Access Layer
├── EVCharging.BE.Common/        # Shared DTOs and Enums
└── EVChargingStation.sln        # Solution file
```

### Technology Stack
- **.NET 9.0** - Latest .NET framework
- **ASP.NET Core Web API** - RESTful API framework
- **Entity Framework Core 8.0** - ORM with SQL Server
- **SignalR** - Real-time communication
- **JWT Bearer Authentication** - Secure authentication
- **Swagger/OpenAPI** - API documentation
- **BCrypt** - Password hashing
- **MailKit** - Email services
- **QRCoder** - QR code generation
- **RestSharp** - HTTP client for external APIs

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- SQL Server (LocalDB or full instance)
- Visual Studio 2022 or VS Code
- Git

### Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd EVChargingStation
   ```

2. **Open the solution**
   ```bash
   # Using Visual Studio
   start EVChargingStation.sln
   
   # Or using VS Code
   code .
   ```

3. **Configure the database**
   - Update the connection string in `appsettings.json`
   - Run Entity Framework migrations:
   ```bash
   cd EVCharging.BE.API
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   cd EVCharging.BE.API
   dotnet run
   ```

5. **Access the API**
   - API: `http://localhost:5167`
   - Swagger UI: `http://localhost:5167/swagger`
   - SignalR Hub: `http://localhost:5167/chargingHub`

## 📚 API Documentation

### Authentication
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login
- `POST /api/auth/logout` - User logout
- `GET /api/auth/profile` - Get user profile

### Charging Management
- `POST /api/charging-sessions/start` - Start charging session
- `POST /api/charging-sessions/stop` - Stop charging session
- `GET /api/charging-sessions/{id}` - Get session details
- `GET /api/charging-sessions/active` - Get active sessions

### Station Management
- `GET /api/charging-stations` - List all stations
- `GET /api/charging-stations/{id}` - Get station details
- `GET /api/charging-points` - List charging points
- `GET /api/charging-points/{id}` - Get point details

### Payment System
- `POST /api/payments` - Create payment
- `POST /api/payments/vnpay` - VNPay payment
- `POST /api/payments/momo` - MoMo payment
- `POST /api/payments/wallet` - Wallet payment
- `GET /api/payments/my-payments` - User's payment history

### Reservations
- `POST /api/reservations` - Create reservation
- `GET /api/reservations/my-reservations` - User's reservations
- `POST /api/reservations/{id}/cancel` - Cancel reservation

## 🔧 Configuration

### Database Connection
Update the connection string in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EVChargingDB;Trusted_Connection=true;"
  }
}
```

### JWT Configuration
Configure JWT settings in `appsettings.json`:
```json
{
  "JWT": {
    "Secret": "your-super-secret-key-here",
    "ValidIssuer": "EVChargingAPI",
    "ValidAudience": "EVChargingUsers"
  }
}
```

### Email Configuration
Configure SMTP settings for email notifications:
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

## 🧪 Testing

### Using Swagger UI
1. Navigate to `http://localhost:5167/swagger`
2. Use the "Authorize" button to set your JWT token
3. Test API endpoints directly from the interface

### Using HTTP Files
The project includes test files:
- `AuthTest.http` - Authentication tests
- `ChargingSessionsTest.http` - Charging session tests
- `PaymentsTest.http` - Payment system tests

## 🔐 Security Features

- **JWT Token Authentication** - Secure token-based auth
- **Password Hashing** - BCrypt for password security
- **Role-based Authorization** - Fine-grained permissions
- **CORS Configuration** - Cross-origin request handling
- **Input Validation** - Comprehensive request validation
- **SQL Injection Protection** - Entity Framework parameterized queries

## 📊 Monitoring & Analytics

- **Real-time Session Monitoring** - Live charging session updates
- **Usage Analytics** - Station and user analytics
- **Payment Analytics** - Revenue and transaction tracking
- **Incident Reporting** - Issue tracking and management
- **Performance Metrics** - System performance monitoring

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

For support and questions:
- Create an issue in the repository
- Check the API documentation in Swagger UI
- Review the authentication guide: `AUTHENTICATION_GUIDE.md`
- Review the payment system guide: `PAYMENT_SYSTEM_GUIDE.md`

## 🚀 Deployment

### Production Deployment
1. Update connection strings for production database
2. Configure production JWT secrets
3. Set up email service credentials
4. Configure payment gateway settings
5. Deploy to your preferred hosting platform

### Docker Support
```bash
# Build Docker image
docker build -t evcharging-api .

# Run container
docker run -p 8080:80 evcharging-api
```

---

**Built with ❤️ for the future of electric mobility**