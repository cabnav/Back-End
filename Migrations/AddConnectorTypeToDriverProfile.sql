-- Migration: Add ConnectorType to DriverProfile
-- Date: 2024
-- Description: Thêm field connector_type vào bảng DriverProfile để validate compatibility khi sạc

-- Step 1: Thêm column connector_type
ALTER TABLE DriverProfile
ADD connector_type NVARCHAR(50) NULL;

-- Step 2: (Optional) Update existing records nếu có thể infer từ VehicleModel
-- Uncomment và customize theo nhu cầu

/*
-- Ví dụ: Tesla thường dùng CCS2
UPDATE DriverProfile
SET connector_type = 'CCS2'
WHERE VehicleModel LIKE '%Tesla%' AND connector_type IS NULL;

-- Ví dụ: Nissan Leaf dùng CHAdeMO
UPDATE DriverProfile
SET connector_type = 'CHAdeMO'
WHERE (VehicleModel LIKE '%Nissan%' OR VehicleModel LIKE '%Leaf%') 
  AND connector_type IS NULL;

-- Ví dụ: BMW i3, iX dùng CCS2
UPDATE DriverProfile
SET connector_type = 'CCS2'
WHERE VehicleModel LIKE '%BMW%' AND connector_type IS NULL;

-- Ví dụ: Hyundai, Kia dùng CCS2
UPDATE DriverProfile
SET connector_type = 'CCS2'
WHERE (VehicleModel LIKE '%Hyundai%' OR VehicleModel LIKE '%Kia%') 
  AND connector_type IS NULL;
*/

-- Step 3: (Optional) Thêm index nếu cần query theo connector_type thường xuyên
-- CREATE INDEX IX_DriverProfile_ConnectorType ON DriverProfile(connector_type);

PRINT 'Migration completed: connector_type added to DriverProfile';

