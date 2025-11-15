-- Migration: Add Driver Profile Approval Fields
-- Description: Thêm các fields để hỗ trợ workflow approval driver vào Corporate
-- Date: 2024

USE [YourDatabaseName]; -- ⚠️ Thay đổi tên database của bạn
GO

-- 1. Thêm các columns mới vào DriverProfile table
ALTER TABLE DriverProfile
ADD status NVARCHAR(20) NULL DEFAULT 'active',
    created_at DATETIME NULL DEFAULT GETDATE(),
    updated_at DATETIME NULL,
    approved_by_user_id INT NULL,
    approved_at DATETIME NULL;
GO

-- 2. Tạo index để query nhanh hơn
CREATE INDEX IX_DriverProfile_CorporateId_Status 
    ON DriverProfile(corporate_id, status);
GO

-- 3. Tạo foreign key cho approved_by_user_id
ALTER TABLE DriverProfile
ADD CONSTRAINT FK__DriverPro__approved_by_user_id 
    FOREIGN KEY (approved_by_user_id) REFERENCES [User](user_id);
GO

-- 4. Update các records hiện có (set status = 'active' cho các drivers không có CorporateId)
UPDATE DriverProfile
SET status = 'active',
    created_at = GETDATE()
WHERE status IS NULL;
GO

-- 5. Update các records có CorporateId nhưng status = 'active' (set về 'pending' để test)
-- ⚠️ UNCOMMENT dòng này nếu bạn muốn set tất cả drivers có CorporateId về 'pending'
-- UPDATE DriverProfile
-- SET status = 'pending'
-- WHERE corporate_id IS NOT NULL AND status = 'active';
-- GO

-- Verify migration
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'DriverProfile'
    AND COLUMN_NAME IN ('status', 'created_at', 'updated_at', 'approved_by_user_id', 'approved_at')
ORDER BY ORDINAL_POSITION;
GO

PRINT 'Migration completed successfully!';
GO

