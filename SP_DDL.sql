USE GeoSupportDev;
GO

-- SP | CREATE OUTPUT TABLE
CREATE PROCEDURE [dbo].[sp_CreateTbl]
AS
BEGIN
	IF OBJECT_ID('[dbo].[IISLogEvents]', N'U') IS NULL
	BEGIN
		CREATE TABLE [dbo].[IISLogEvents] (
			RowId INT IDENTITY(1,1),
			Id NVARCHAR(100) NULL,
			MessageType NVARCHAR(100) NULL,
			[Owner] NVARCHAR(50) NULL,
			LogGroup NVARCHAR(50) NULL,
			LogStream NVARCHAR(50) NULL,
			SubscriptionFilters NVARCHAR(50) NULL,
			[DateTime] DATETIME NULL,
			RequestMessage NVARCHAR(MAX) NULL
		);
	END
END
GO
--

-- [dbo].[GetServerRequestMessage]
-- reverse engineering this from db alter...
CREATE PROCEDURE [dbo].[GetServerRequestMessage]
	@inDate Date = NULL,
	@inMonth INT = NULL,
	@inYear INT = NULL,
	@inUserAccessKey NVARCHAR(20) = NULL,
	@inServerMessage NVARCHAR(3) = NULL,
	@inStartServerIP NVARCHAR(16) = NULL
AS
BEGIN
	SELECT 
		[DateTime],
		SUBSTRING(RequestMessage, CHARINDEX('200 0 0 ', RequestMessage), 20) AS ServerResponseMessage,
		SUBSTRING(RequestMessage, CHARINDEX('', RequestMessage)+20, 14) AS ServerIP,
		SUBSTRING(RequestMessage, CHARINDEX('Key=', RequestMessage)+4, 16) AS UserKey,
		SUBSTRING(RequestMessage, CHARINDEX('Function_', RequestMessage), 12) AS FunctionCall,
		RequestMessage
	FROM [dbo].[IISLogEvents]
	WHERE
		SUBSTRING(RequestMessage, CHARINDEX(',+', RequestMessage), 15) LIKE '%,+%'
			AND SUBSTRING(RequestMessage, CHARINDEX('200 0 0 ', RequestMessage), 20) LIKE '%200 0 0%'
			AND (RequestMessage LIKE '%200 0 0 '+@inServerMessage+'%'
				OR @inServerMessage IS NULL)
			AND (CONVERT(DATE, [DateTime]) = @inDate
				OR @inDate IS NULL)
			AND (YEAR([DateTime]) = @inYear
				OR @inYear IS NULL)
			AND (MONTH([DateTime]) = @inMonth
				OR @inMonth IS NULL)
			AND	(SUBSTRING(RequestMessage, CHARINDEX('', RequestMessage)+20, 15) LIKE '%'+@inStartServerIP+'%'
				OR @inStartServerIP IS NULL)
	ORDER BY [DateTime] DESC;
END
GO
--

-- [dbo].[GetUserHourlyUsageReport]
-- reverse engineering this from db alter...
CREATE PROCEDURE [dbo].[GetUserHourlyUsageReport]
	@inDate Date = NULL,
	@inMonth INT = NULL,
	@inYear INT = NULL,
	@inUserAccessKey NVARCHAR(20) = NULL
AS
BEGIN
	DECLARE @KeyUrlParam CHAR(4) = 'Key=';
	DECLARE @KeyLen INT = 16;

	SELECT
		CONVERT(DATE, [DateTime]) AS [Date],
		DATEPART(HOUR, [DateTime]) AS [Hour],
		SUBSTRING(RequestMessage, CHARINDEX(@KeyUrlParam, RequestMessage)+4, @KeyLen) AS UserKey,
		COUNT(*) AS CallCount
	FROM [dbo].[IISLogEvents]
	WHERE
		 SUBSTRING(RequestMessage, CHARINDEX(@KeyUrlParam, RequestMessage), 20) LIKE '%key=%'
			AND SUBSTRING(RequestMessage, CHARINDEX(',+', RequestMessage), 15) LIKE '%,+%'
			AND SUBSTRING(RequestMessage, CHARINDEX(@KeyUrlParam, RequestMessage)+4, @KeyLen) NOT LIKE '%key %'
			AND (CONVERT(DATE, [DateTime]) = @inDate
				OR @inDate IS NULL)
			AND (YEAR([DateTime]) = @inYear
				OR @inYear IS NULL)
			AND (MONTH([DateTime]) = @inMonth
				OR @inMonth IS NULL)
			AND (SUBSTRING(RequestMessage, CHARINDEX(@KeyUrlParam, RequestMessage)+4, @KeyLen) = @inUserAccessKey
				OR @inUserAccessKey IS NULL)
	GROUP BY
		CONVERT(DATE, [DateTime]),
		DATEPART(HOUR, [DateTime]),
		SUBSTRING(RequestMessage, CHARINDEX(@KeyUrlParam, RequestMessage)+4, @KeyLen)
	ORDER BY
		[Date] DESC,
		[Hour] ASC,
		CallCount DESC;
END
GO
--
