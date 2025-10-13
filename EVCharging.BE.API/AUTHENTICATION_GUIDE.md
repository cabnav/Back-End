# EV Charging Station Management System - Authentication Guide

## Overview
This guide explains how to implement and use the authentication system for the EV Charging Station Management System.

## Features Implemented

### 1. User Registration
- **Endpoint**: `POST /api/auth/register`
- **Description**: Register new users with different roles (driver, admin, cs_staff)
- **Request Body**:
```json
{
  "name": "John Doe",
  "email": "john.doe@example.com",
  "password": "password123",
  "phone": "+1234567890",
  "role": "driver",
  "licenseNumber": "DL123456789",
  "vehicleModel": "Tesla Model 3",
  "vehiclePlate": "ABC-123",
  "batteryCapacity": 75
}
```

### 2. User Login
- **Endpoint**: `POST /api/auth/login`
- **Description**: Authenticate users and return JWT token
- **Request Body**:
```json
{
  "email": "john.doe@example.com",
  "password": "password123"
}
```

### 3. User Logout
- **Endpoint**: `POST /api/auth/logout`
- **Description**: Logout user and invalidate token
- **Headers**: `Authorization: Bearer <token>`

### 4. Token Validation
- **Endpoint**: `POST /api/auth/validate`
- **Description**: Validate if token is still valid
- **Headers**: `Authorization: Bearer <token>`

### 5. Get User Profile
- **Endpoint**: `GET /api/auth/profile`
- **Description**: Get current user information
- **Headers**: `Authorization: Bearer <token>`

## User Roles

### 1. Driver (`driver`)
- Can book charging sessions
- Can view charging stations
- Can make payments
- Has driver profile with vehicle information

### 2. Admin (`admin`)
- Full system access
- Can manage all users and stations
- Can view analytics and reports

### 3. Charging Station Staff (`cs_staff`)
- Can manage on-site charging sessions
- Can handle on-site payments
- Can report issues

## Security Features

### 1. Password Hashing
- Passwords are hashed using SHA256
- No plain text passwords stored in database

### 2. JWT Token Authentication
- Tokens expire after 24 hours
- Tokens include user ID, email, and role
- Blacklist mechanism for logout

### 3. Token Blacklisting
- Logged out tokens are added to blacklist
- Blacklisted tokens cannot be used for authentication

## Database Schema

### User Table
```sql
CREATE TABLE Users (
    UserId int IDENTITY(1,1) PRIMARY KEY,
    Name nvarchar(255) NOT NULL,
    Email nvarchar(255) NOT NULL UNIQUE,
    Password nvarchar(255) NOT NULL,
    Phone nvarchar(20),
    Role nvarchar(50) NOT NULL,
    WalletBalance decimal(10,2),
    BillingType nvarchar(50),
    MembershipTier nvarchar(50),
    CreatedAt datetime2
);
```

### DriverProfile Table
```sql
CREATE TABLE DriverProfiles (
    DriverProfileId int IDENTITY(1,1) PRIMARY KEY,
    UserId int FOREIGN KEY REFERENCES Users(UserId),
    LicenseNumber nvarchar(50),
    VehicleModel nvarchar(100),
    VehiclePlate nvarchar(20),
    BatteryCapacity int
);
```

## Configuration

### JWT Settings (appsettings.json)
```json
{
  "JWT": {
    "Secret": "your-secret-key-here",
    "ValidIssuer": "https://localhost:7035/",
    "ValidAudience": "https://localhost:7035"
  }
}
```

## Testing

### Using the provided HTTP file
1. Open `AuthTest.http` in your IDE
2. Run the application
3. Execute the test requests in order
4. Copy the token from login response for authenticated requests

### Manual Testing
1. **Register**: Create a new user account
2. **Login**: Get authentication token
3. **Validate**: Check if token is valid
4. **Profile**: Get user information
5. **Logout**: Invalidate token

## Error Handling

### Common Error Responses
- **400 Bad Request**: Missing required fields
- **401 Unauthorized**: Invalid credentials or expired token
- **500 Internal Server Error**: Server-side errors

### Example Error Response
```json
{
  "message": "Invalid email or password"
}
```

## Implementation Steps

### 1. Database Setup
- Ensure database connection string is correct
- Run migrations to create tables
- Seed initial data if needed

### 2. Service Registration
- AuthService is registered in Program.cs
- JWT authentication is configured
- Authorization policies are set up

### 3. API Usage
- Use the provided HTTP file for testing
- Implement frontend integration
- Handle token storage and refresh

## Best Practices

### 1. Token Management
- Store tokens securely (HttpOnly cookies recommended)
- Implement token refresh mechanism
- Handle token expiration gracefully

### 2. Password Security
- Enforce strong password policies
- Consider implementing password reset functionality
- Use HTTPS in production

### 3. Error Handling
- Don't expose sensitive information in error messages
- Log authentication attempts for security monitoring
- Implement rate limiting for login attempts

## Next Steps

1. **Password Reset**: Implement forgot password functionality
2. **Email Verification**: Add email verification for new accounts
3. **Two-Factor Authentication**: Add 2FA for enhanced security
4. **Role-Based Authorization**: Implement fine-grained permissions
5. **Audit Logging**: Track all authentication events

## Support

For issues or questions regarding the authentication system, please refer to the API documentation or contact the development team.
