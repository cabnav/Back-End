# Contributing to EV Charging Station Management System

Thank you for your interest in contributing to the EV Charging Station Management System! This document provides guidelines for contributing to this project.

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code
- SQL Server (LocalDB or full instance)
- Git

### Development Setup
1. Fork the repository
2. Clone your fork: `git clone https://github.com/your-username/EVChargingStation.git`
3. Create a feature branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Test your changes thoroughly
6. Commit your changes: `git commit -m 'Add some feature'`
7. Push to your branch: `git push origin feature/your-feature-name`
8. Open a Pull Request

## 📋 Development Guidelines

### Code Style
- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and single-purpose
- Use async/await for I/O operations

### Architecture
- Follow the existing layered architecture:
  - **API Layer** (`EVCharging.BE.API`) - Controllers and API endpoints
  - **Services Layer** (`EVCharging.BE.Services`) - Business logic
  - **DAL Layer** (`EVCharging.BE.DAL`) - Data access
  - **Common Layer** (`EVCharging.BE.Common`) - DTOs and shared code

### Testing
- Write unit tests for new features
- Test API endpoints using the provided HTTP files
- Ensure all existing tests pass
- Test with different user roles (Driver, Admin, Station Staff)

## 🐛 Bug Reports

When reporting bugs, please include:
- Clear description of the issue
- Steps to reproduce
- Expected vs actual behavior
- Environment details (.NET version, OS, etc.)
- Screenshots if applicable

## ✨ Feature Requests

When requesting features, please include:
- Clear description of the feature
- Use case and benefits
- Any implementation ideas
- Priority level

## 📝 Pull Request Process

1. **Create a detailed description** of your changes
2. **Link related issues** if applicable
3. **Ensure all tests pass** before submitting
4. **Update documentation** if needed
5. **Request review** from maintainers

### PR Template
```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review completed
- [ ] Documentation updated
- [ ] No breaking changes (or clearly documented)
```

## 🏗️ Project Structure

```
EVChargingStation/
├── EVCharging.BE.API/          # Web API Layer
│   ├── Controllers/            # API Controllers
│   ├── Hubs/                   # SignalR Hubs
│   └── Program.cs              # Application startup
├── EVCharging.BE.Services/     # Business Logic Layer
│   └── Services/               # Service implementations
├── EVCharging.BE.DAL/           # Data Access Layer
│   ├── Entities/               # Database entities
│   └── Repository/             # Repository pattern
├── EVCharging.BE.Common/        # Shared DTOs and Enums
└── README.md                   # Project documentation
```

## 🔧 Development Tools

### Recommended Extensions (VS Code)
- C# Dev Kit
- .NET Install Tool
- REST Client
- GitLens

### Recommended Extensions (Visual Studio)
- Entity Framework Core Power Tools
- ReSharper (optional)

## 📚 Resources

- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/)
- [SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)

## 🤝 Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/0/code_of_conduct/).

## 📞 Getting Help

- Create an issue for questions
- Check existing documentation
- Review the API documentation in Swagger UI
- Contact maintainers for urgent issues

Thank you for contributing! 🎉
