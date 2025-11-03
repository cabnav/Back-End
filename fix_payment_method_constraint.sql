-- ========================================
-- SỬA CHECK CONSTRAINT CHO payment_method
-- ========================================

USE EVChargingManagement;
GO

-- 1. Tìm và xem CHECK constraint hiện tại cho payment_method
SELECT 
    cc.name AS ConstraintName,
    cc.definition AS ConstraintDefinition,
    OBJECT_NAME(cc.parent_object_id) AS TableName
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.Payment')
AND cc.definition LIKE '%payment_method%';
GO

-- 2. Xóa CHECK constraint cũ (nếu có)
DECLARE @constraintName NVARCHAR(200);

SELECT @constraintName = name
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Payment')
AND definition LIKE '%payment_method%';

IF @constraintName IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX);
    SET @sql = 'ALTER TABLE dbo.Payment DROP CONSTRAINT ' + QUOTENAME(@constraintName);
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
-- - "wallet" (thanh toán bằng ví)
-- - "momo" (thanh toán qua MoMo)
-- - "cash" (thanh toán bằng tiền mặt)
-- - "mock" (MockPay - dành cho test)

IF NOT EXISTS (
    SELECT 1 
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.Payment')
    AND name = 'CK_Payment_payment_method'
)
BEGIN
    ALTER TABLE dbo.Payment
    ADD CONSTRAINT CK_Payment_payment_method
    CHECK (
        payment_method IS NULL 
        OR payment_method IN ('wallet', 'momo', 'cash', 'mock')
    );
    
    PRINT 'Đã thêm CHECK constraint mới cho phép: wallet, momo, cash, mock';
END
ELSE
BEGIN
    PRINT 'CHECK constraint CK_Payment_payment_method đã tồn tại';
END
GO

-- 4. Kiểm tra lại constraint
SELECT 
    name AS ConstraintName,
    definition AS ConstraintDefinition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID('dbo.Payment')
AND name = 'CK_Payment_payment_method';
GO

PRINT 'Hoàn tất!';
GO

