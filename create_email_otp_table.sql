-- =========================================================
-- Create EmailOTP Table for Email Verification
-- =========================================================

USE EVChargingManagement;
GO

-- Create EmailOTP table
CREATE TABLE EmailOTP (
    otp_id INT IDENTITY(1,1) PRIMARY KEY,
    email NVARCHAR(255) NOT NULL,
    otp_code NVARCHAR(6) NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT GETDATE(),
    expires_at DATETIME2 NOT NULL,
    is_used BIT NOT NULL DEFAULT 0,
    purpose NVARCHAR(50) DEFAULT 'registration' CHECK (purpose IN ('registration', 'reset_password', 'change_email'))
);

GO

-- Add index for email lookups
CREATE INDEX IX_EmailOTP_Email ON EmailOTP(email);

-- Add index for active OTPs
CREATE INDEX IX_EmailOTP_Active ON EmailOTP(email, is_used, expires_at);

GO

PRINT 'EmailOTP table created successfully!';

