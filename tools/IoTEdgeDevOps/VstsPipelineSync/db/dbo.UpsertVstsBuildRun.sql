﻿CREATE PROCEDURE [dbo].[UpsertVstsBuildRun]
	@BuildNumber varchar(20),
	@DefinitionId int,
	@DefinitionName varchar(100),
	@SourceBranch varchar(100),
	@SourceVersionDisplayUri varchar(500),
	@WebUri varchar(500),
	@Status varchar(20),
	@Result varchar(20),
	@QueueTime datetime2,
	@StartTime datetime2,
	@FinishTime datetime2
AS
	DECLARE @now datetime2;
	SET @now = SYSDATETIME();

	IF EXISTS (SELECT 1 FROM dbo.VstsBuildRun WHERE BuildNumber = @BuildNumber AND DefinitionId = @DefinitionId)
	BEGIN
		UPDATE dbo.VstsBuildRun
		SET DefinitionName = @DefinitionName,
		    SourceBranch = @SourceBranch,
			SourceVersionDisplayUri = @SourceVersionDisplayUri,
			WebUri = @WebUri,
			[Status] = @Status,
			Result = @Result,
			QueueTime = @QueueTime,
			StartTime = @StartTime,
			FinishTime = @FinishTIme,
			UpdateTime = @now
		WHERE BuildNumber = @BuildNumber
		AND DefinitionId = @DefinitionId
	END
	ELSE
	BEGIN
		INSERT INTO dbo.VstsBuildRun(BuildNumber, DefinitionId, DefinitionName, SourceBranch, SourceVersionDisplayUri, WebUri, [Status], Result, QueueTime, StartTime, FinishTime, CreatedTime, UpdateTime)
		VALUES (@BuildNumber, @DefinitionId, @DefinitionName, @SourceBranch, @SourceVersionDisplayUri, @WebUri, @Status, @Result, @QueueTime, @StartTime, @FinishTime, @now, @now)
	END
