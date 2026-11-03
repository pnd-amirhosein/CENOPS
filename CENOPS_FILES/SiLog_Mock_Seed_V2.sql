/*
 CENOPS realistic log seed V2 — [SiLog].dbo.ResponseSiLog
 TEST / DISPOSABLE DATABASE COPIES ONLY. Never execute against production logs.
 Defaults: 14 dates from 7 days ago; Iran weekday pattern, 10:00-14:00 peak.
 Baseline SiLog: Thu/Fri ~300, Sat ~550, Sun ~1250, Mon-Wed ~900-1100 per day.
 SrLog = 2x baseline; UiLog = 4x baseline. Parameter @RowsPerDay scales baseline.
 ~75% success / 12% business / 13% system (more errors in short incidents).
 Re-running APPENDS data; SummeryData identifies all generated rows.
*/
USE [SiLog];
GO
CREATE OR ALTER PROCEDURE dbo.usp_SeedMockSiLog
    @StartDate date = NULL,
    @Days int = 14,
    @RowsPerDay int = 1000,   -- scale baseline (1000 = 100%); 1500 = 150%
    @Seed int = 20261012,
    @ConfirmInsert bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @ConfirmInsert <> 1
        THROW 51000, N'Pass @ConfirmInsert = 1. TEST/MOCK databases only.', 1;
    IF @Days NOT BETWEEN 1 AND 60 OR @RowsPerDay NOT BETWEEN 1 AND 2500
        THROW 51001, N'@Days must be 1..60; @RowsPerDay must be 1..2500.', 1;
    IF @StartDate IS NULL
        SET @StartDate = DATEADD(DAY, -7, CONVERT(date, GETDATE()));

    DECLARE @VolumeMultiplier int = 1;
    DECLARE @RunBase bigint = DATEDIFF_BIG(MILLISECOND, '20200101', SYSUTCDATETIME()) * CONVERT(bigint, 1000000);
    DECLARE @DayIndex int = 0, @TargetRows int, @RowsInserted bigint = 0;
    DECLARE @CurrentDate date, @DayOfWeek int, @BaseRows int, @DailyVariation int;

    DECLARE @Methods TABLE (Ordinal int IDENTITY(1,1) PRIMARY KEY, MethodId int NOT NULL UNIQUE);
    INSERT INTO @Methods(MethodId) VALUES (111001),(111002),(111003),(111004),(111005),(111006),(111007),(111008),(111009),(111011),(111012),(111013),(111014),(111015),(111016),(111017),(111018),(111019),(111020),(111021),(111022),(111023),(111024),(111025),(111026),(111027),(111028),(111031),(111033),(111034);

    WHILE @DayIndex < @Days
    BEGIN
        SET @CurrentDate = DATEADD(DAY, @DayIndex, @StartDate);
        -- 1900-01-01 was Monday; independent of SQL Server language and DATEFIRST.
        SET @DayOfWeek = ((DATEDIFF(DAY, '19000101', @CurrentDate) % 7) + 7) % 7;
        SET @DailyVariation = (CHECKSUM(@Seed, @DayIndex, 29) & 2147483647);
        SET @BaseRows = CASE @DayOfWeek
            WHEN 0 THEN 980 + @DailyVariation % 121   -- Monday: 980-1100
            WHEN 1 THEN 900 + @DailyVariation % 151   -- Tuesday: 900-1050
            WHEN 2 THEN 950 + @DailyVariation % 131   -- Wednesday: 950-1080
            WHEN 3 THEN 270 + @DailyVariation % 61    -- Thursday: 270-330
            WHEN 4 THEN 270 + @DailyVariation % 61    -- Friday: 270-330
            WHEN 5 THEN 500 + @DailyVariation % 101   -- Saturday: 500-600
            ELSE 1200 + @DailyVariation % 101         -- Sunday: 1200-1300
        END;
        SET @TargetRows = CONVERT(int, ROUND(@BaseRows * @VolumeMultiplier * (@RowsPerDay / 1000.0), 0));

        BEGIN TRY
            BEGIN TRANSACTION;

        ;WITH Digits AS
        (
            SELECT d FROM (VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) x(d)
        ),
        Numbers AS
        (
            SELECT TOP (@TargetRows) CONVERT(int, a.d + b.d*10 + c.d*100 + d.d*1000 + e.d*10000) AS n
            FROM Digits a CROSS JOIN Digits b CROSS JOIN Digits c
                 CROSS JOIN Digits d CROSS JOIN Digits e
            ORDER BY a.d + b.d*10 + c.d*100 + d.d*1000 + e.d*10000
        )
        INSERT INTO dbo.ResponseSiLog
        (ServiceId, ServiceMethodId, ServiceOriginalId, MethodOriginalId, MethodOutput, Exception, SummeryData, ErrorCode, ResponseTime, PointerId, RequestId, MethodInput, CallTime, Username, MicroserviceName)
        SELECT 100 + method.MethodId % 6, method.MethodId, 500 + method.MethodId % 6,
            method.MethodId % 1000, resultpayload.OutputJson,
            CASE WHEN statusinfo.StatusKind = 2 THEN errors.ExceptionText ELSE NULL END,
            CONCAT(N'{"dataset":"CENOPS_MOCK_V2","source":"SiLog","status":"', resultinfo.StatusName,
                N'","runId":"', @RunBase, N'","service":"', svc.ServiceName, N'"}'),
            resultinfo.ErrorCode, response.RespondedAt,
            @RunBase + @DayIndex * 100000 + num.n / 4 + 1,
            @RunBase + @DayIndex * 100000 + num.n + 1,
            payload.InputJson, times.CallAt,
            CONCAT(N'staff.', num.n % 140 + 1), svc.ServiceName
        FROM Numbers num
        CROSS APPLY (VALUES (
            (CHECKSUM(@Seed, @DayIndex, num.n, 11) & 2147483647) % 30 + 1,
            (CHECKSUM(@Seed, @DayIndex, num.n, 12) & 2147483647) % 1000,
            (CHECKSUM(@Seed, @DayIndex, num.n, 13) & 2147483647) % 60,
            (CHECKSUM(@Seed, @DayIndex, num.n, 14) & 2147483647) % 1000,
            (CHECKSUM(@Seed, @DayIndex, num.n, 15) & 2147483647) % 100,
            (CHECKSUM(@Seed, @DayIndex, num.n, 16) & 2147483647) % 12,
            (CHECKSUM(@Seed, @DayIndex, num.n, 17) & 2147483647) % 9,
            (CHECKSUM(@Seed, @DayIndex, num.n, 18) & 2147483647) % 1000
        )) rnd(MethodSlot, HourBucket, Minute, Millisecond, StatusBucket, ErrorVariant, PayloadVariant, LatencyJitter)
        JOIN @Methods method ON method.Ordinal = rnd.MethodSlot
        CROSS APPLY (VALUES (
            CASE
                WHEN rnd.HourBucket < 20 THEN rnd.HourBucket / 10      -- 00:00-01:59, ~2%
                WHEN rnd.HourBucket < 40 THEN 2 + (rnd.HourBucket - 20) / 10
                WHEN rnd.HourBucket < 60 THEN 4 + (rnd.HourBucket - 40) / 10
                WHEN rnd.HourBucket < 80 THEN 6 + (rnd.HourBucket - 60) / 10
                WHEN rnd.HourBucket < 120 THEN 8
                WHEN rnd.HourBucket < 190 THEN 9
                WHEN rnd.HourBucket < 350 THEN 10                 -- 10:00-13:59 ~66%
                WHEN rnd.HourBucket < 520 THEN 11
                WHEN rnd.HourBucket < 690 THEN 12
                WHEN rnd.HourBucket < 850 THEN 13
                WHEN rnd.HourBucket < 920 THEN 14
                WHEN rnd.HourBucket < 950 THEN 15
                WHEN rnd.HourBucket < 970 THEN 16
                WHEN rnd.HourBucket < 980 THEN 17
                WHEN rnd.HourBucket < 985 THEN 18
                WHEN rnd.HourBucket < 990 THEN 19
                WHEN rnd.HourBucket < 993 THEN 20
                WHEN rnd.HourBucket < 996 THEN 21
                WHEN rnd.HourBucket < 998 THEN 22
                ELSE 23
            END
        )) clock(HourOfDay)
        CROSS APPLY (VALUES (
            CASE WHEN method.MethodId IN (111007,111014,111018,111025,111031)
                      AND ((@DayIndex + 1) % 5 IN (0,1))
                      AND clock.HourOfDay BETWEEN 10 AND 14
                THEN CASE WHEN rnd.StatusBucket < 35 THEN 0 WHEN rnd.StatusBucket < 60 THEN 1 ELSE 2 END
                ELSE CASE WHEN rnd.StatusBucket < 75 THEN 0 WHEN rnd.StatusBucket < 87 THEN 1 ELSE 2 END
            END
        )) statusinfo(StatusKind)
        CROSS APPLY (VALUES (
            CASE statusinfo.StatusKind WHEN 0 THEN 0 WHEN 1 THEN 1001 + rnd.ErrorVariant ELSE 5001 + rnd.ErrorVariant END,
            CASE statusinfo.StatusKind WHEN 0 THEN N'success' WHEN 1 THEN N'business' ELSE N'system' END,
            CASE statusinfo.StatusKind WHEN 2 THEN 1500 + rnd.LatencyJitter * 8
                 WHEN 1 THEN 90 + rnd.LatencyJitter * 2
                 ELSE 15 + rnd.LatencyJitter / 2 END
        )) resultinfo(ErrorCode, StatusName, DurationMs)
        CROSS APPLY (VALUES (
            CASE method.MethodId % 6
                WHEN 0 THEN N'Customs.Clearance'
                WHEN 1 THEN N'Trade.Orders'
                WHEN 2 THEN N'Catalog.Goods'
                WHEN 3 THEN N'Gateway.Requests'
                WHEN 4 THEN N'Identity.Service'
                ELSE N'Payments.Service' END
        )) svc(ServiceName)
        CROSS APPLY (VALUES (
            DATEADD(MILLISECOND, rnd.Millisecond,
                DATEADD(MINUTE, clock.HourOfDay * 60 + rnd.Minute,
                    CONVERT(datetime2(7), @CurrentDate)))
        )) times(CallAt)
        CROSS APPLY (VALUES (DATEADD(MILLISECOND, resultinfo.DurationMs, times.CallAt))) response(RespondedAt)
        CROSS APPLY (VALUES (
            CASE rnd.PayloadVariant
                WHEN 0 THEN CONCAT(N'{"sourceCurrencyCode":', 100 + num.n % 8, N',"goodsCode":', 20000 + num.n % 900, N',"requestedAmount":', 1 + num.n % 300, N'}')
                WHEN 1 THEN CONCAT(N'{"orderId":', 100000 + num.n % 900000, N',"action":"confirm","warehouseCode":', 10 + num.n % 20, N'}')
                WHEN 2 THEN CONCAT(N'{"trackingCode":"NT-', 400000 + num.n % 600000, N'","nationalCode":"', 1000000000 + num.n % 900000000, N'"}')
                WHEN 3 THEN CONCAT(N'{"page":', 1 + num.n % 12, N',"pageSize":', 10 + (num.n % 4)*10, N',"sortBy":"createdAt","descending":true}')
                WHEN 4 THEN CONCAT(N'{"invoiceId":', 300000 + num.n % 300000, N',"amount":', 50000 + num.n % 2000000, N',"currency":"IRR"}')
                WHEN 5 THEN CONCAT(N'{"goodsIds":[', 1000 + num.n % 900, N',', 1001 + num.n % 900, N'],"includeInactive":false}')
                WHEN 6 THEN CONCAT(N'{"username":"staff.', 1 + num.n % 140, N'","includePermissions":true}')
                WHEN 7 THEN CONCAT(N'{"dateFrom":"2026-10-01","dateTo":"2026-10-12","filter":"pending","branch":', 1 + num.n % 9, N'}')
                ELSE CONCAT(N'{"shipmentId":', 700000 + num.n % 50000, N',"status":"released","quantity":', 1 + num.n % 200, N'}')
            END
        )) payload(InputJson)
        CROSS APPLY (VALUES (
            CASE rnd.ErrorVariant
                WHEN 0 THEN N'System.NullReferenceException: Object reference not set to an instance of an object. at Customs.Clearance.Services.ReleaseService.ApplyAsync()'
                WHEN 1 THEN N'System.TimeoutException: The operation has timed out waiting for the upstream service response.'
                WHEN 2 THEN N'System.Threading.Tasks.TaskCanceledException: A task was canceled. at System.Net.Http.HttpClient.SendAsync()'
                WHEN 3 THEN N'System.Net.Http.HttpRequestException: Response status code does not indicate success: 502 (Bad Gateway).'
                WHEN 4 THEN N'Microsoft.Data.SqlClient.SqlException: Execution Timeout Expired. The timeout period elapsed prior to completion of the operation.'
                WHEN 5 THEN N'System.InvalidOperationException: Sequence contains no matching element. at System.Linq.Enumerable.First()'
                WHEN 6 THEN N'System.Net.Sockets.SocketException: No connection could be made because the target machine actively refused it.'
                WHEN 7 THEN N'System.OperationCanceledException: The operation was canceled by a cancellation token.'
                WHEN 8 THEN N'System.Text.Json.JsonException: The JSON value could not be converted to the expected request model.'
                WHEN 9 THEN N'System.IO.IOException: Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host.'
                WHEN 10 THEN N'System.UnauthorizedAccessException: Access to the requested resource is denied.'
                ELSE N'System.ArgumentNullException: Value cannot be null. (Parameter ''request'')'
            END
        )) errors(ExceptionText)
        CROSS APPLY (VALUES (
            CASE rnd.ErrorVariant % 6
                WHEN 0 THEN N'Goods record was not found for the supplied virtual goods code.'
                WHEN 1 THEN N'The requested clearance quantity exceeds the registered quantity.'
                WHEN 2 THEN N'Currency source is inactive or has expired.'
                WHEN 3 THEN N'Duplicate request: this operation has already been processed.'
                WHEN 4 THEN N'Order is in a state that does not allow this operation.'
                ELSE N'The supplied identifier does not match the registered ownership information.'
            END
        )) business(BusinessText)
        CROSS APPLY (VALUES (
            CASE statusinfo.StatusKind
                WHEN 0 THEN CONCAT(N'{"success":true,"data":{"operationId":', @RunBase + @DayIndex * 100000 + num.n,
                    N',"processed":', num.n % 20 + 1, N',"status":"completed"},"elapsedMs":', resultinfo.DurationMs, N'}')
                WHEN 1 THEN CONCAT(N'{"success":false,"error":{"code":', resultinfo.ErrorCode,
                    N',"type":"validation","message":"', business.BusinessText, N'"}}')
                ELSE CONCAT(N'{"success":false,"error":{"code":', resultinfo.ErrorCode,
                    N',"type":"internal","message":"An unexpected error occurred","traceId":"mock-', @DayIndex, N'-', num.n, N'"}}')
            END
        )) resultpayload(OutputJson)

            SET @RowsInserted += @@ROWCOUNT;
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
        SET @DayIndex += 1;
    END;

    SELECT DB_NAME() AS DatabaseName, 'dbo.ResponseSiLog' AS TargetTable,
           @RowsInserted AS InsertedRows, @StartDate AS StartDateInclusive,
           DATEADD(DAY,@Days,@StartDate) AS EndDateExclusive,
           @VolumeMultiplier AS SourceMultiplier, @RunBase AS RunId;
END;
GO

-- LOCAL/TEST only, after inspecting the target database:
-- EXEC dbo.usp_SeedMockSiLog @ConfirmInsert = 1;
-- EXEC dbo.usp_SeedMockSiLog @RowsPerDay = 1500, @ConfirmInsert = 1;

-- Read-only verification:
-- SELECT CAST(CallTime AS date) AS [Day], COUNT_BIG(*) AS [Rows],
--   SUM(CASE WHEN ErrorCode = 0 THEN 1 ELSE 0 END) AS [Success],
--   SUM(CASE WHEN ErrorCode <> 0 AND Exception IS NULL THEN 1 ELSE 0 END) AS [BusinessErrors],
--   SUM(CASE WHEN ErrorCode <> 0 AND Exception IS NOT NULL THEN 1 ELSE 0 END) AS [SystemErrors]
-- FROM dbo.ResponseSiLog WHERE SummeryData LIKE N'%"dataset":"CENOPS_MOCK_V2"%'
-- GROUP BY CAST(CallTime AS date) ORDER BY [Day];

-- Cleanup TEST data only (review affected rows before executing):
-- DELETE FROM dbo.ResponseSiLog WHERE SummeryData LIKE N'%"dataset":"CENOPS_MOCK_V2"%';
