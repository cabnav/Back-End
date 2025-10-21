# Changelog

All notable changes to the EV Charging Station Management System will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial project setup
- Core authentication system
- Charging session management
- Payment gateway integration (VNPay, MoMo)
- Real-time notifications via SignalR
- Reservation system
- Admin dashboard functionality
- Comprehensive API documentation

### Changed
- Refactored service layer structure
- Improved dependency injection configuration
- Enhanced error handling

### Fixed
- SignalR dependency injection issues
- Nullable reference type warnings
- Build configuration

## [1.0.0] - 2024-10-21

### Added
- Complete EV charging station management system
- Multi-role user system (Driver, Admin, Station Staff)
- JWT authentication with role-based authorization
- Real-time charging session monitoring
- Multiple payment methods integration
- Email notification system
- QR code generation for reservations
- Comprehensive API with Swagger documentation
- GitHub repository setup with CI/CD pipeline

### Technical Features
- .NET 9.0 with ASP.NET Core Web API
- Entity Framework Core with SQL Server
- SignalR for real-time communication
- JWT Bearer authentication
- BCrypt password hashing
- MailKit for email services
- QRCoder for QR code generation
- RestSharp for external API calls

---

**Note**: This changelog will be updated as new features are added and bugs are fixed.
