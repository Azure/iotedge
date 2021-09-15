SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[UpsertVstsReleaseDeployment]
    @Id int,
    @ReleaseEnvironmentId int,
    @Attempt int,
    @Status varchar(20),
    @LastModifiedOn datetime2
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();
    
    IF EXISTS (SELECT 1 FROM dbo.VstsReleaseDeployment WHERE [Id] = @Id)
    BEGIN
        UPDATE dbo.VstsReleaseDeployment
        SET [ReleaseEnvironmentId] = @ReleaseEnvironmentId,
    	    [Attempt] = @Attempt,
    	    [Status] = @Status,
    	    [LastModifiedOn] = @LastModifiedOn,
    	    UpdatedAt = @now
    	WHERE [Id] = @Id
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsReleaseDeployment([Id], ReleaseEnvironmentId, Attempt, [Status], LastModifiedOn, InsertedAt, UpdatedAt)
        VALUES (@Id, @ReleaseEnvironmentId, @Attempt, @Status, @LastModifiedOn, @now, @now)
    END
GO

