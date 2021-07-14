SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[UpsertVstsReleaseEnvironment]
    @Id int,
	@ReleaseId int,
	@DefinitionId int,
	@DefinitionName varchar(100),
	@Status varchar(50)
AS
	DECLARE @now datetime2;
	SET @now = SYSDATETIME();

	IF EXISTS (SELECT 1 FROM dbo.VstsReleaseEnvironment WHERE [Id] = @Id)
	BEGIN
		UPDATE dbo.VstsReleaseEnvironment
		SET [ReleaseId] = @ReleaseId,
		    [DefinitionId] = @DefinitionId,
		    [DefinitionName] = @DefinitionName,
		    [Status] = @Status,
			UpdatedAt = @now
		WHERE [Id] = @Id
	END
	ELSE
	BEGIN
		INSERT INTO dbo.VstsReleaseEnvironment([Id], ReleaseId, DefinitionId, DefinitionName, [Status], InsertedAt, UpdatedAt)
		VALUES (@Id, @ReleaseId, @DefinitionId, @DefinitionName, @Status, @now, @now)
	END
GO

