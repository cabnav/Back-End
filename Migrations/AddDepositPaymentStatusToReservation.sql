-- Migration: Add deposit_payment_status column to Reservation table
-- Date: 2025-11-24
-- Description: Thêm cột deposit_payment_status để theo dõi trạng thái thanh toán tiền cọc

-- Check if column already exists
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Reservation]') 
    AND name = 'deposit_payment_status'
)
BEGIN
    -- Add column
    ALTER TABLE [dbo].[Reservation]
    ADD deposit_payment_status NVARCHAR(20) NULL;
    
    PRINT '✅ Column deposit_payment_status added to Reservation table';
END
ELSE
BEGIN
    PRINT '⚠️ Column deposit_payment_status already exists in Reservation table';
END
GO

-- Update existing reservations: Set status based on existing deposit payments
UPDATE r
SET r.deposit_payment_status = p.payment_status
FROM [dbo].[Reservation] r
INNER JOIN [dbo].[Payment] p ON r.reservation_id = p.reservation_id
WHERE p.payment_type = 'deposit'
  AND p.payment_status IN ('success', 'pending', 'failed')
  AND r.deposit_payment_status IS NULL;

PRINT '✅ Updated deposit_payment_status for existing reservations based on payment records';
GO

