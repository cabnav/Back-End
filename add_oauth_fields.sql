-- =========================================================
-- Add OAuth Support Fields to User Table
-- =========================================================

USE EVChargingManagement;
GO

-- Add OAuth fields to User table
ALTER TABLE [User]
ADD 
    provider NVARCHAR(50) NULL,           -- 'google', 'facebook', etc. or NULL for regular users
    provider_id NVARCHAR(255) NULL,       -- External provider user ID
    email_verified BIT DEFAULT 0;         -- Email verification status

GO

-- Add index for OAuth lookups
CREATE INDEX IX_User_Provider_ProviderId ON [User](provider, provider_id) WHERE provider IS NOT NULL;

GO

-- Add constraint to ensure provider_id exists when provider is set
ALTER TABLE [User]
ADD CONSTRAINT CK_User_OAuth_Consistency
CHECK (
    (provider IS NULL AND provider_id IS NULL) OR
    (provider IS NOT NULL AND provider_id IS NOT NULL)
);

GO

PRINT 'OAuth fields added successfully to User table!';

