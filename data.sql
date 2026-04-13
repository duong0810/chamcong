-- =============================================
-- Script: Tạo Database AttendanceDB HOÀN CHỈNH
-- Phiên bản: 2.1 - GPS Precision FIX
-- Mô tả: Hệ thống chấm công bằng khuôn mặt
-- =============================================

USE master;
GO

-- Xóa database cũ nếu tồn tại
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'AttendanceDB')
BEGIN
    ALTER DATABASE AttendanceDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE AttendanceDB;
    PRINT '🗑️  Đã xóa database cũ';
END
GO

-- Tạo database mới
CREATE DATABASE AttendanceDB
COLLATE SQL_Latin1_General_CP1_CI_AS;
GO

PRINT '✅ Đã tạo database AttendanceDB';
GO

USE AttendanceDB;
GO


-- =============================================
-- BẢNG 1: EMPLOYEES (Nhân viên)
-- =============================================
CREATE TABLE Employees (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeCode NVARCHAR(50) NOT NULL UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PhoneNumber NVARCHAR(20) NULL,
    Department NVARCHAR(100) NULL,
    Position NVARCHAR(100) NULL,
    FaceDescriptor NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE INDEX IX_Employees_EmployeeCode ON Employees(EmployeeCode);
CREATE INDEX IX_Employees_IsActive ON Employees(IsActive);

PRINT '✅ Đã tạo bảng Employees';
GO

-- =============================================
-- BẢNG 2: ATTENDANCERECORDS (Chấm công) - GPS PRECISION FIX
-- =============================================
CREATE TABLE AttendanceRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId INT NOT NULL,
    CheckInTime DATETIME NOT NULL,
    CheckOutTime DATETIME NULL,
    Location NVARCHAR(100) NULL,
    
    -- ✅ GPS COORDINATES - ĐỔI SANG FLOAT ĐỂ LƯU ĐẦY ĐỦ CHỮ SỐ THẬP PHÂN
    Latitude FLOAT NULL,      -- Thay vì DECIMAL(18, 8)
    Longitude FLOAT NULL,     -- Thay vì DECIMAL(18, 8)
    Accuracy FLOAT NULL,
    
    DeviceInfo NVARCHAR(200) NULL,
    FaceMatchScore FLOAT NULL,
    Notes NVARCHAR(500) NULL,
    IsManualEntry BIT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_AttendanceRecords_Employees 
        FOREIGN KEY (EmployeeId) 
        REFERENCES Employees(Id) 
        ON DELETE CASCADE
);

CREATE INDEX IX_AttendanceRecords_EmployeeId ON AttendanceRecords(EmployeeId);
CREATE INDEX IX_AttendanceRecords_CheckInTime ON AttendanceRecords(CheckInTime);
CREATE INDEX IX_AttendanceRecords_GPS ON AttendanceRecords(Latitude, Longitude) 
    WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;

PRINT '✅ Đã tạo bảng AttendanceRecords (GPS với FLOAT precision)';
GO

-- =============================================
-- SEED DATA: Nhân viên mẫu
-- =============================================
INSERT INTO Employees (EmployeeCode, FullName, Email, PhoneNumber, Department, Position, IsActive, CreatedDate)
VALUES 
    ('NV005', N'Lê Minh Đương', 'lmd@company.com', '0372284930', 'IT', 'Developer', 1, '2026-03-26');

    ('NV001', N'Nguyễn Văn A', 'nva@company.com', '0901234567', 'IT', 'Developer', 1, '2026-03-27'),
    ('NV002', N'Trần Thị B', 'ttb@company.com', '0912345678', 'HR', 'Manager', 1, '2026-03-27'),
    ('NV003', N'Lê Văn C', 'lvc@company.com', '0923456789', 'IT', 'Tester', 1, '2026-03-27'),
	('NV004', N'Thommy', 'thommy@company.com', '0908651333', 'CEO', 'Manager', 1, '2026-03-26'),
    ('NV005', N'Lê Minh Đương', 'lmd@company.com', '0372284930', 'IT', 'Developer', 1, '2026-03-26'),
	('NV006', N'Phan Lê Thanh Ngân', 'pltn@company.com', '0903485219', 'IT', 'Developer', 1, '2026-03-26'),
	('NV007', N'Lê Thị Mỹ Tiên', 'ltmt@company.com', '0903485219', 'IT', 'Developer', 1, '2026-03-26');
PRINT '✅ Đã thêm 3 nhân viên mẫu';
GO

-- =============================================
-- STORED PROCEDURE: Kiểm tra chấm công hôm nay
-- =============================================
CREATE PROCEDURE sp_CheckTodayAttendance
    @EmployeeId INT
AS
BEGIN
    SELECT TOP 1 
        Id, 
        EmployeeId, 
        CheckInTime, 
        CheckOutTime,
        Location,
        Latitude,
        Longitude,
        Accuracy,
        FaceMatchScore
    FROM AttendanceRecords
    WHERE EmployeeId = @EmployeeId
      AND CAST(CheckInTime AS DATE) = CAST(GETDATE() AS DATE)
    ORDER BY CheckInTime DESC;
END
GO

PRINT '✅ Đã tạo stored procedure sp_CheckTodayAttendance';
GO

-- =============================================
-- VIEW: Tổng quan chấm công hôm nay
-- =============================================
CREATE VIEW vw_TodayAttendance AS
SELECT 
    e.EmployeeCode,
    e.FullName,
    e.Department,
    a.CheckInTime,
    a.CheckOutTime,
    a.Location,
    a.Latitude,
    a.Longitude,
    a.Accuracy,
    a.FaceMatchScore,
    CASE 
        WHEN a.CheckOutTime IS NULL THEN N'Đang làm việc'
        ELSE N'Đã ra về'
    END AS Status,
    DATEDIFF(HOUR, a.CheckInTime, ISNULL(a.CheckOutTime, GETDATE())) AS WorkHours,
    CASE 
        WHEN a.Latitude IS NOT NULL AND a.Longitude IS NOT NULL 
        THEN CONCAT('GPS: ', CAST(a.Latitude AS VARCHAR(20)), ', ', CAST(a.Longitude AS VARCHAR(20)))
        ELSE N'Chưa có GPS'
    END AS GPSInfo
FROM Employees e
INNER JOIN AttendanceRecords a ON e.Id = a.EmployeeId
WHERE CAST(a.CheckInTime AS DATE) = CAST(GETDATE() AS DATE);
GO

PRINT '✅ Đã tạo view vw_TodayAttendance';
GO

IF OBJECT_ID('dbo.CompanySettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanySettings
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CompanyLocationUrl NVARCHAR(1000) NULL,
        CompanyLat FLOAT NULL,
        CompanyLng FLOAT NULL,
        LocationAcceptThresholdMeters FLOAT NULL,
        UpdatedAt DATETIME2 NULL
    );

    INSERT INTO dbo.CompanySettings (CompanyLocationUrl, CompanyLat, CompanyLng, LocationAcceptThresholdMeters, UpdatedAt)
    VALUES ('https://www.google.com/maps/place/C%C3%94NG+TY+TNHH+KOSMOS+DEVELOPMENT/@10.8029435,106.5586313,17z', 10.8029435, 106.5612062, 40.0, SYSUTCDATETIME());

    PRINT '✅ Table dbo.CompanySettings created and initial row inserted';
END
ELSE
    PRINT 'ℹ️ Table dbo.CompanySettings already exists';

ALTER TABLE AttendanceRecords
ADD 
    CheckOutLatitude FLOAT NULL,
    CheckOutLongitude FLOAT NULL,
    CheckOutAccuracy FLOAT NULL;
GO

USE AttendanceDB;
GO

-- Thêm IsManualEntry nếu chưa có (mặc định 0)
IF COL_LENGTH('dbo.AttendanceRecords', 'IsManualEntry') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD IsManualEntry BIT NOT NULL CONSTRAINT DF_AttendanceRecords_IsManualEntry DEFAULT (0);
    PRINT '✅ Added column IsManualEntry with default 0';
END
ELSE
BEGIN
    PRINT 'ℹ️ Column IsManualEntry already exists';
END
GO


-- Thêm DistanceMeters nếu chưa có (nullable)
IF COL_LENGTH('dbo.AttendanceRecords', 'DistanceMeters') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD DistanceMeters FLOAT NULL;
    PRINT '✅ Added column DistanceMeters (FLOAT NULL)';
END
ELSE
BEGIN
    PRINT 'ℹ️ Column DistanceMeters already exists';
END
GO



USE AttendanceDB;
GO

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckInDeviceInfo') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords ADD CheckInDeviceInfo NVARCHAR(500) NULL;
    PRINT '✅ Added column CheckInDeviceInfo';
END
ELSE
    PRINT 'ℹ️ Column CheckInDeviceInfo already exists';

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckOutDeviceInfo') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords ADD CheckOutDeviceInfo NVARCHAR(500) NULL;
    PRINT '✅ Added column CheckOutDeviceInfo';
END
ELSE
    PRINT 'ℹ️ Column CheckOutDeviceInfo already exists';



-- 1) Thêm cột nếu chưa có
IF COL_LENGTH('dbo.AttendanceRecords', 'CheckInIp') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords ADD CheckInIp NVARCHAR(45) NULL;
    PRINT '✅ Added column CheckInIp';
END
ELSE
    PRINT 'ℹ️ Column CheckInIp already exists';

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckOutIp') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords ADD CheckOutIp NVARCHAR(45) NULL;
    PRINT '✅ Added column CheckOutIp';
END
ELSE
    PRINT 'ℹ️ Column CheckOutIp already exists';
GO

USE AttendanceDB;
GO

PRINT '🔒 Kiểm tra quyền và backup DB trước khi thay đổi';

ALTER TABLE dbo.AttendanceRecords
ADD LivenessPassed BIT NULL,
    DetectionScore FLOAT NULL;



-- 1) Thêm cột FaceImageUrl (URL của ảnh đã lưu)
IF COL_LENGTH('dbo.AttendanceRecords', 'CheckInFaceImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckInFaceImageUrl NVARCHAR(500) NULL;
    PRINT '✅ Added column CheckInFaceImageUrl';
END
ELSE
    PRINT 'ℹ️ Column CheckInFaceImageUrl already exists';

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckOutFaceImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckOutFaceImageUrl NVARCHAR(500) NULL;
    PRINT '✅ Added column CheckOutFaceImageUrl';
END
ELSE
    PRINT 'ℹ️ Column CheckOutFaceImageUrl already exists';

-- 2) Thêm cột CheckInNetwork / CheckOutNetwork (ví dụ "wifi","4g","3g","unknown")
IF COL_LENGTH('dbo.AttendanceRecords', 'CheckInNetwork') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckInNetwork NVARCHAR(50) NULL;
    PRINT '✅ Added column CheckInNetwork';
END
ELSE
    PRINT 'ℹ️ Column CheckInNetwork already exists';

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckOutNetwork') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckOutNetwork NVARCHAR(50) NULL;
    PRINT '✅ Added column CheckOutNetwork';
END
ELSE
    PRINT 'ℹ️ Column CheckOutNetwork already exists';

-- 3) (Tuỳ chọn) Thêm ClientDeviceIdHash để lưu hash device id (không lưu raw id)
IF COL_LENGTH('dbo.AttendanceRecords', 'ConnectionType') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD ConnectionType NVARCHAR(128) NULL;
    PRINT '✅ Added column ConnectionType';
END
ELSE
    PRINT 'ℹ️ Column ConnectionType already exists';

-- Backup DB trước khi chạy!

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckInDistanceMeters') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckInDistanceMeters FLOAT NULL;
    PRINT '✅ Added column CheckInDistanceMeters';
END
ELSE
    PRINT 'ℹ️ Column CheckInDistanceMeters already exists';

IF COL_LENGTH('dbo.AttendanceRecords', 'CheckOutDistanceMeters') IS NULL
BEGIN
    ALTER TABLE dbo.AttendanceRecords
    ADD CheckOutDistanceMeters FLOAT NULL;
    PRINT '✅ Added column CheckOutDistanceMeters';
END
ELSE
    PRINT 'ℹ️ Column CheckOutDistanceMeters already exists';

-- 4) Tạo index nhẹ nếu bạn muốn filter bằng network nhanh
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_AttendanceRecords_CheckInNetwork' AND object_id = OBJECT_ID('dbo.AttendanceRecords'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AttendanceRecords_CheckInNetwork
    ON dbo.AttendanceRecords(CheckInNetwork);
    PRINT '✅ Created index IX_AttendanceRecords_CheckInNetwork';
END
ELSE
    PRINT 'ℹ️ Index IX_AttendanceRecords_CheckInNetwork already exists';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_AttendanceRecords_CheckOutNetwork' AND object_id = OBJECT_ID('dbo.AttendanceRecords'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AttendanceRecords_CheckOutNetwork
    ON dbo.AttendanceRecords(CheckOutNetwork);
    PRINT '✅ Created index IX_AttendanceRecords_CheckOutNetwork';
END
ELSE
    PRINT 'ℹ️ Index IX_AttendanceRecords_CheckOutNetwork already exists';

GO

PRINT '🔎 Kiểm tra vài record mẫu để xác nhận';
SELECT TOP 20
    Id, EmployeeId, CheckInTime, CheckOutTime,
    DeviceInfo, CheckInIp, CheckOutIp,
    FaceImageUrl, CheckInNetwork, CheckOutNetwork, ClientDeviceIdHash
FROM dbo.AttendanceRecords
ORDER BY Id DESC;
GO

PRINT 'ℹ️ Nếu cần rollback, chạy ALTER TABLE ... DROP COLUMN <ColumnName>';


-- =============================================
-- KIỂM TRA KẾT QUẢ
-- =============================================
PRINT '';
PRINT '========================================';
PRINT '✅✅✅ TẠO DATABASE THÀNH CÔNG!';
PRINT '========================================';
PRINT 'Database: AttendanceDB';
PRINT 'Bảng: Employees, AttendanceRecords';
PRINT 'GPS: Latitude (FLOAT), Longitude (FLOAT), Accuracy (FLOAT)';
PRINT '========================================';
GO

-- Hiển thị danh sách nhân viên
SELECT 'DANH SÁCH NHÂN VIÊN' AS Info;
SELECT 
    Id,
    EmployeeCode,
    FullName,
    Email,
    Department,
    Position
FROM Employees;
GO

-- Hiển thị cấu trúc bảng AttendanceRecords
SELECT 'CẤU TRÚC BẢNG ATTENDANCERECORDS' AS Info;
SELECT 
    COLUMN_NAME AS [Cột],
    DATA_TYPE AS [Kiểu],
    IS_NULLABLE AS [NULL?],
    ISNULL(CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR), 
           ISNULL(CAST(NUMERIC_PRECISION AS VARCHAR) + ',' + CAST(NUMERIC_SCALE AS VARCHAR), '-')) AS [Kích thước]
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AttendanceRecords'
ORDER BY ORDINAL_POSITION;
GO

PRINT '========================================';
PRINT 'SẴN SÀNG SỬ DỤNG!';
PRINT 'Connection String:';
PRINT 'Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;';
PRINT '========================================';
GO


USE AttendanceDB;
GO

PRINT '✅ Đã xóa dữ liệu chấm công cũ';
PRINT '📍 Bây giờ hãy chấm công lại để lưu GPS chính xác';
GO

-- Kiểm tra đã xóa chưa
SELECT COUNT(*) AS [Số bản ghi còn lại] FROM AttendanceRecords;


select FaceDescriptor from Employees;

DELETE FROM Employees
DELETE FROM AttendanceRecords;

select * from AttendanceRecords
select * from Employees

select * from CompanySettings
delete from CompanySettings

SELECT TOP 20 Id,FullName,FaceDescriptor
FROM dbo.Employees 
ORDER BY Id DESC;

SELECT TOP 20 Id,EmployeeId, FaceMatchScore
FROM dbo.AttendanceRecords 
ORDER BY Id DESC;

SELECT TOP 20 Id,EmployeeId,Notes
FROM dbo.AttendanceRecords 
ORDER BY Id DESC;


