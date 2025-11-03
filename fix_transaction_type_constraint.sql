-- ========================================
-- SỬA CHECK CONSTRAINT CHO transaction_type
-- ========================================

USE EVChargingManagement;
GO

-- 1. Tìm và xem CHECK constraint hiện tại
SELECT 
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition,
    OBJECT_NAME(cc.parent_object_id) AS TableName
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.WalletTransaction')
AND cc.definition LIKE '%transaction_type%';
GO

-- 2. Xóa CHECK constraint cũ (nếu có)
DECLARE @constraintName NVARCHAR(200);

SELECT @constraintName = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.WalletTransaction')
AND definition LIKE '%transaction_type%';

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX);
    SET @sql = 'ALTER TABLE dbo.WalletTransaction DROP CONSTRAINT ' + QUOTENAME(@constraintName);
    EXEC sp_executesql @sql;
    PRINT 'Đã xóa CHECK constraint cũ: ' + @constraintName;
END
ELSE
BEGIN
    PRINT 'Không tìm thấy CHECK constraint cũ';
END
GO

-- 3. Thêm CHECK constraint mới cho phép các giá trị hợp lệ
-- Dựa trên code, các giá trị được sử dụng:
-- - "top_up" (nạp tiền)
-- - "topup" (nạp tiền - MockPay)
-- - "debit" (trừ tiền - thanh toán/cọc)
-- - "credit" (có thể dùng trong tương lai)

IF NOT EXISTS (
    SELECT 1 
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.WalletTransaction')
    AND name = 'CK_WalletTransaction_TransactionType'
)
BEGIN
    ALTER TABLE dbo.WalletTransaction
    ADD CONSTRAINT CK_WalletTransaction_TransactionType
    CHECK (
        transaction_type IS NULL 
        OR transaction_type IN ('top_up', 'topup', 'debit', 'credit')
    );
    
    PRINT 'Đã thêm CHECK constraint mới cho phép: top_up, topup, debit, credit';
END
ELSE
BEGIN
    PRINT 'CHECK constraint CK_WalletTransaction_TransactionType đã tồn tại';
END
GO

-- 4. Kiểm tra lại constraint
SELECT 
    name AS ConstraintName,
    definition AS ConstraintDefinition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.WalletTransaction')
AND name = 'CK_WalletTransaction_TransactionType';
GO

PRINT 'Hoàn tất!';
GO

