-- Run AFTER: dotnet ef database update ... (tables must already exist).
-- Idempotent starter permissions for NTSW CENOPS / SQL Server.
-- Re-running it re-grants the initial baseline to Owner/Admin/User.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Obsolete module is retired; preserve historical rows without granting visibility.
UPDATE dbo.Accesses SET IsDeleted = 1 WHERE [Key] = N'pages.discrepancy-goods';

INSERT INTO dbo.Accesses ([Key], [Name], [Description], [IsDeleted])
SELECT seed.[Key], seed.[Name], seed.[Description], 0
FROM (VALUES
    (N'pages.dashboard', N'داشبورد', N'نمایش داشبورد'),
    (N'pages.contacts', N'مخاطبین', N'صفحه مخاطبین'),
    (N'pages.health-apis', N'سرویس‌های مانیتورینگ', N'صفحه مانیتورینگ'),
    (N'wiki.curls.view', N'کتابخانه cURL', N'فهرست درخواست‌های API'),
    (N'wiki.queries.view', N'کتابخانه SQL', N'فهرست پرس‌وجوهای SQL'),
    (N'pages.weather', N'آب‌وهوا', N'صفحه آب‌وهوا'),
    (N'settings.view', N'مدیریت سیستم', N'نمایش تنظیمات و مدیریت کاربران و نقش‌ها'),
    (N'settings.access.manage', N'مدیریت دسترسی‌ها', N'مشاهده و تغییر مجوزهای نقش‌ها')
) AS seed ([Key], [Name], [Description])
WHERE NOT EXISTS (SELECT 1 FROM dbo.Accesses a WHERE a.[Key] = seed.[Key] AND a.IsDeleted = 0);

INSERT INTO dbo.RoleAccesses (RoleId, AccessId)
SELECT r.Id, a.Id FROM dbo.AspNetRoles r
CROSS JOIN dbo.Accesses a
WHERE r.IsDeleted = 0 AND a.IsDeleted = 0 AND
(
    (r.NormalizedName IN (N'USER', N'ADMIN', N'OWNER') AND a.[Key] IN
     (N'pages.dashboard', N'pages.contacts', N'pages.health-apis', N'wiki.curls.view', N'wiki.queries.view', N'pages.weather'))
    OR (r.NormalizedName IN (N'ADMIN', N'OWNER') AND a.[Key] = N'settings.view')
    OR (r.NormalizedName = N'OWNER' AND a.[Key] = N'settings.access.manage')
)
AND NOT EXISTS (SELECT 1 FROM dbo.RoleAccesses ra WHERE ra.RoleId = r.Id AND ra.AccessId = a.Id);

COMMIT TRANSACTION;
