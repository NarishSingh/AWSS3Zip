USE GeoSupportDev;
GO

SELECT TOP 1000
    *
FROM [dbo].IISLogEvents
ORDER BY [DateTime];
GO