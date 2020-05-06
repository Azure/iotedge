CREATE PROCEDURE [dbo].[UpsertVstsBuild]
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

	IF EXISTS (SELECT 1 FROM dbo.VstsBuild WHERE BuildNumber = @BuildNumber AND DefinitionId = @DefinitionId)
	BEGIN
		UPDATE dbo.VstsBuild
		SET DefinitionName = @DefinitionName,
		    SourceBranch = @SourceBranch,
			SourceVersionDisplayUri = @SourceVersionDisplayUri,
			WebUri = @WebUri,
			[Status] = @Status,
			Result = @Result,
			QueueTime = @QueueTime,
			StartTime = @StartTime,
			FinishTime = @FinishTIme,
			UpdatedAt = @now
		WHERE BuildNumber = @BuildNumber
		AND DefinitionId = @DefinitionId
	END
	ELSE
	BEGIN
		INSERT INTO dbo.VstsBuild(BuildNumber, DefinitionId, DefinitionName, SourceBranch, SourceVersionDisplayUri, WebUri, [Status], Result, QueueTime, StartTime, FinishTime, InsertedAt, UpdatedAt)
		VALUES (@BuildNumber, @DefinitionId, @DefinitionName, @SourceBranch, @SourceVersionDisplayUri, @WebUri, @Status, @Result, @QueueTime, @StartTime, @FinishTime, @now, @now)
	END
