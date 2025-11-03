-- =====================================================
-- Add PaymentType column to Payment table
-- Mục đích: Phân biệt các loại thanh toán (đặt cọc, thanh toán session, hoàn tiền, etc.)
-- =====================================================

-- Kiểm tra và thêm cột payment_type vào bảng Payment
IF NOT EXISTS (
    SELECT * 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Payment]') 
    AND name = 'payment_type'
)
BEGIN
    ALTER TABLE [dbo].[Payment]
    ADD [payment_type] NVARCHAR(30) NULL;
    
    PRINT 'Column payment_type added successfully';
END
ELSE
BEGIN
    PRINT 'Column payment_type already exists';
END
GO

-- Tạo index để tối ưu query theo payment_type
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE name = 'IX_Payment_PaymentType' 
    AND object_id = OBJECT_ID(N'[dbo].[Payment]')
)
BEGIN
    CREATE INDEX [IX_Payment_PaymentType] 
    ON [dbo].[Payment]([payment_type]);
    
    PRINT 'Index IX_Payment_PaymentType created successfully';
END
ELSE
BEGIN
    PRINT 'Index IX_Payment_PaymentType already exists';
END
GO

-- Thêm index composite để query deposit theo reservation
IF NOT EXISTS (
    SELECT * 
    FROM sys.indexes 
    WHERE name = 'IX_Payment_ReservationType' 
    AND object_id = OBJECT_ID(N'[dbo].[Payment]')
)
BEGIN
    CREATE INDEX [IX_Payment_ReservationType] 
    ON [dbo].[Payment]([reservation_id], [payment_type])
    WHERE [reservation_id] IS NOT NULL;
    
    PRINT 'Index IX_Payment_ReservationType created successfully';
END
ELSE
BEGIN
    PRINT 'Index IX_Payment_ReservationType already exists';
END
GO

-- Cập nhật các giá trị mặc định cho payment_type dựa trên dữ liệu hiện tại (optional)
-- Nếu payment có reservation_id thì có thể là deposit hoặc session_payment
-- Nếu payment có session_id thì là session_payment
-- UPDATE [dbo].[Payment]
-- SET [payment_type] = CASE 
--     WHEN [session_id] IS NOT NULL THEN 'session_payment'
--     WHEN [reservation_id] IS NOT NULL THEN 'deposit'  -- Giả định, cần review lại
--     ELSE 'other'
-- END
-- WHERE [payment_type] IS NULL;
-- GO

PRINT 'PaymentType column migration completed!';
PRINT 'PaymentType values: deposit, session_payment, refund, top_up, etc.';
GO

