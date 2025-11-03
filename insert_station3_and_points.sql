-- =====================================================
-- Insert Station 3 và Charging Points
-- Mục đích: Thêm trạm sạc thứ 3 và các điểm sạc cho trạm này
-- =====================================================

-- Kiểm tra xem Station 3 đã tồn tại chưa
IF NOT EXISTS (SELECT * FROM [ChargingStation] WHERE [station_id] = 3)
BEGIN
    -- Insert Station 3
    INSERT INTO [ChargingStation] (
        [name],
        [address],
        [latitude],
        [longitude],
        [operator],
        [status],
        [total_points],
        [available_points]
    )
    VALUES (
        'EV Station District 3',
        '88 Nguyen Dinh Chieu, District 3, HCMC',
        10.7865,
        106.6876,
        'EVCharge Vietnam',
        'active',
        6,
        4
    );

    PRINT 'Station 3 inserted successfully';
END
ELSE
BEGIN
    PRINT 'Station 3 already exists';
END
GO

-- Lấy StationId của Station 3 (giả sử là 3, hoặc query từ DB)
DECLARE @Station3Id INT;
SET @Station3Id = (SELECT [station_id] FROM [ChargingStation] WHERE [name] = 'EV Station District 3');

-- Insert Charging Points cho Station 3
IF @Station3Id IS NOT NULL
BEGIN
    -- Kiểm tra xem các points đã tồn tại chưa (dựa vào QR code)
    
    -- Point 1: CCS2 50kW
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_001')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'CCS2',
            50,
            3500.00,
            'available',
            'QR_D3_001',
            0.0,
            CAST(DATEADD(DAY, -30, GETDATE()) AS DATE)
        );
    END

    -- Point 2: CCS2 50kW
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_002')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'CCS2',
            50,
            3500.00,
            'available',
            'QR_D3_002',
            0.0,
            CAST(DATEADD(DAY, -25, GETDATE()) AS DATE)
        );
    END

    -- Point 3: CHAdeMO 50kW
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_003')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'CHAdeMO',
            50,
            3400.00,
            'available',
            'QR_D3_003',
            0.0,
            CAST(DATEADD(DAY, -20, GETDATE()) AS DATE)
        );
    END

    -- Point 4: Type2 22kW
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_004')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'Type2',
            22,
            3000.00,
            'available',
            'QR_D3_004',
            0.0,
            CAST(DATEADD(DAY, -15, GETDATE()) AS DATE)
        );
    END

    -- Point 5: CCS2 150kW (Fast Charging)
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_005')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'CCS2',
            150,
            4500.00,
            'available',
            'QR_D3_005',
            0.0,
            CAST(DATEADD(DAY, -10, GETDATE()) AS DATE)
        );
    END

    -- Point 6: CCS2 50kW (Occupied - để test)
    IF NOT EXISTS (SELECT * FROM [ChargingPoint] WHERE [qr_code] = 'QR_D3_006')
    BEGIN
        INSERT INTO [ChargingPoint] (
            [station_id],
            [connector_type],
            [power_output],
            [price_per_kwh],
            [status],
            [qr_code],
            [current_power],
            [last_maintenance]
        )
        VALUES (
            @Station3Id,
            'CCS2',
            50,
            3500.00,
            'in_use',
            'QR_D3_006',
            42.5,
            CAST(DATEADD(DAY, -5, GETDATE()) AS DATE)
        );
    END

    -- Cập nhật available_points dựa trên số points có status = 'available'
    UPDATE [ChargingStation]
    SET [available_points] = (
        SELECT COUNT(*)
        FROM [ChargingPoint]
        WHERE [station_id] = @Station3Id 
        AND [status] = 'available'
    )
    WHERE [station_id] = @Station3Id;

    PRINT 'Charging Points for Station 3 inserted successfully';
    PRINT 'Available points count updated';
END
ELSE
BEGIN
    PRINT 'Station 3 not found. Please insert station first.';
END
GO

-- Kiểm tra kết quả
SELECT 
    s.[station_id],
    s.[name] AS station_name,
    s.[status] AS station_status,
    s.[total_points],
    s.[available_points],
    COUNT(cp.[point_id]) AS actual_points_count
FROM [ChargingStation] s
LEFT JOIN [ChargingPoint] cp ON s.[station_id] = cp.[station_id]
WHERE s.[station_id] = 3
GROUP BY s.[station_id], s.[name], s.[status], s.[total_points], s.[available_points];
GO

SELECT 
    cp.[point_id],
    cp.[station_id],
    cp.[connector_type],
    cp.[power_output],
    cp.[price_per_kwh],
    cp.[status],
    cp.[qr_code]
FROM [ChargingPoint] cp
WHERE cp.[station_id] = 3
ORDER BY cp.[point_id];
GO

