-- ! FIXME make these stored procedures

-- SELECT ALL
SELECT TOP 1000 
	* 
FROM [dbo].[IISLogEvents];
GO

-- KEY STATS - see key and request record
SELECT 
	*
FROM (
	SELECT 	
		DateTime, 
		RequestMessage,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'Key='), 16)  AS UserKey,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'GET '), LEN(SUBSTRING(RequestMessage, 1, CHARINDEX(RequestMessage, ' 80')))) AS HttpRequest 
	FROM [dbo].[IISLogEvents]
) AS KeysRequests
WHERE RequestMessage like '%Geoservice/Geoservice.svc%' AND UserKey like '%Key=%';
GO

-- 
-- ! FIXME
SELECT
	*
FROM (
	SELECT
		RequestMessage,
		SUBSTRING(RequestMessage, CHARINDEX(RequestMessage, 'Key='), 16) AS UserKey, 
		COUNT(*) AS CallCt
	FROM [dbo].[IISLogEvents]
) AS KeysCounts
WHERE RequestMessage like '%Geoservice/Geoservice.svc%' AND UserKey like '%Key=%'
GROUP BY UserKey
ORDER BY CallCt DESC;
GO