USE GeoSupportDev;
GO

-- SELECT ALL
SELECT
	*
FROM [dbo].[IISLogEvents];
GO
--

-- SELECT USER KEY BY `python-requests/` user agent
SELECT
	SUBSTRING(RequestMessage, CHARINDEX('&Key=', RequestMessage), (16 + LEN('&Key='))) AS UserKey,
	COUNT(*) AS TotalRequests
FROM [dbo].[IISLogEvents]
WHERE CHARINDEX('python-requests/', RequestMessage) > 0
GROUP BY 
	SUBSTRING(RequestMessage, CHARINDEX('&Key=', RequestMessage), (16 + LEN('&Key=')))
GO
--

-- KEY STATS - see key and request record
-- FIXME
SELECT 
	*
FROM (
	SELECT
		[DateTime],
		RequestMessage,
		SUBSTRING(RequestMessage, CHARINDEX('&Key=', RequestMessage), (16 + LEN('&Key='))) AS UserKey,
		SUBSTRING(RequestMessage, CHARINDEX('GET ', RequestMessage), LEN(SUBSTRING(RequestMessage, 1, CHARINDEX(RequestMessage, ' 80')))) AS HttpRequest
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
