USE GeoSupportDev;
GO

-- SELECT ALL
SELECT
	*
FROM [dbo].[IISLogEvents];
GO
--

-- KEY STATS - see key and request record
SELECT 
	*
FROM (
	SELECT
		[DateTime],
		RequestMessage,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'Key='), 16)  AS UserKey,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'GET '), LEN(SUBSTRING(RequestMessage, 1, CHARINDEX(RequestMessage, ' 80')))) AS HttpRequest
	FROM [dbo].[IISLogEvents]
) AS KeysRequests
WHERE RequestMessage LIKE '%Geoservice/Geoservice.svc%'
	AND UserKey LIKE '%Key=%';
GO
-- 

-- CALL COUNTS BY KEY
SELECT
	*
FROM (
	SELECT
		RequestMessage,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'Key='), 16) AS UserKey,
		COUNT(*) AS CallCount
	FROM [dbo].[IISLogEvents]
) AS KeysCounts
WHERE RequestMessage LIKE '%Geoservice/Geoservice.svc%'
	AND UserKey LIKE '%Key=%'
GROUP BY UserKey
ORDER BY CallCount DESC;
GO
--

EXEC [dbo].[sp_CreateTbl];
GO
--

DROP PROCEDURE [dbo].[sp_CreateTbl];
GO
--
