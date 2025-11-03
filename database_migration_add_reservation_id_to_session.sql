-- Migration: Add ReservationId column to ChargingSession table
-- Date: 2025-01-XX
-- Description: Add ReservationId column to link charging sessions with reservations

-- Step 1: Add ReservationId column (nullable, as sessions can exist without reservations)
ALTER TABLE [dbo].[ChargingSession]
ADD [reservation_id] INT NULL;

-- Step 2: Add foreign key constraint
ALTER TABLE [dbo].[ChargingSession]
ADD CONSTRAINT [FK__ChargingS__reservation__7B5B524D]
FOREIGN KEY ([reservation_id])
REFERENCES [dbo].[Reservation] ([reservation_id]);

-- Step 3: Add index for better query performance
CREATE INDEX [IX_ChargingSession_ReservationId]
ON [dbo].[ChargingSession] ([reservation_id]);

-- Verify: Check if column was added successfully
-- SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
-- FROM INFORMATION_SCHEMA.COLUMNS
-- WHERE TABLE_NAME = 'ChargingSession' AND COLUMN_NAME = 'reservation_id';

