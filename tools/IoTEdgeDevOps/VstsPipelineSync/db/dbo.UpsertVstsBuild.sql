CREATE PROCEDURE [dbo].[UpsertVstsBuild]
	@BuildId varchar(20),
	@BuildNumber varchar(20),
	@SourceVersion varchar(100),
	@DefinitionId int,
	@DefinitionName varchar(100),
	@SourceBranch varchar(100),
	@SourceVersionDisplayUri varchar(500),
	@WebUri varchar(500),
	@Status varchar(20),
	@Result varchar(20),
	@QueueTime datetime2,
	@StartTime datetime2,
	@FinishTime datetime2,
	@WasScheduled varchar(20)
AS
	DECLARE @now datetime2;
	SET @now = SYSDATETIME();

	IF EXISTS (SELECT 1 FROM dbo.VstsBuild WHERE BuildId = @BuildId)
	BEGIN
		UPDATE dbo.VstsBuild
		SET BuildId = @BuildId,
		    SourceVersion = @SourceVersion,
		    DefinitionName = @DefinitionName,
		    SourceBranch = @SourceBranch,
			SourceVersionDisplayUri = @SourceVersionDisplayUri,
			WebUri = @WebUri,
			[Status] = @Status,
			Result = @Result,
			QueueTime = @QueueTime,
			StartTime = @StartTime,
			FinishTime = @FinishTIme,
			WasScheduled = @WasScheduled,
			UpdatedAt = @now
		WHERE BuildId = @BuildId
	END
	ELSE
	BEGIN
		INSERT INTO dbo.VstsBuild(BuildId, BuildNumber, DefinitionId, DefinitionName, SourceBranch, SourceVersion, SourceVersionDisplayUri, WebUri, [Status], Result, QueueTime, StartTime, FinishTime, WasScheduled, InsertedAt, UpdatedAt)
		VALUES (@BuildId, @BuildNumber, @DefinitionId, @DefinitionName, @SourceBranch, @SourceVersion, @SourceVersionDisplayUri, @WebUri, @Status, @Result, @QueueTime, @StartTime, @FinishTime, @WasScheduled, @now, @now)
	END
