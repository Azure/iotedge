SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[UpsertVstsReleaseTask]
    @ReleaseDeploymentId int,
    @Id int,
    @Name varchar(100),
    @Status varchar(20),
    @StartTime datetime2,
    @FinishTime datetime2,
    @LogUrl varchar(500) = NULL
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();
    
    IF EXISTS (SELECT 1 FROM dbo.VstsReleaseTask WHERE ReleaseDeploymentId = @ReleaseDeploymentId AND [Id] = @Id)
    BEGIN
        UPDATE dbo.VstsReleaseTask
        SET [Name] = @Name,
            [Status] = @Status,
            [StartTime] = @StartTime,
            [FinishTime] = @StartTime,
            [LogUrl] = @LogUrl,
            UpdatedAt = @now
        WHERE ReleaseDeploymentId = @ReleaseDeploymentId AND [Id] = @Id
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsReleaseTask(ReleaseDeploymentId, [Id], [Name], [Status], StartTime, FinishTime, LogUrl, InsertedAt, UpdatedAt)
        VALUES (@ReleaseDeploymentId, @Id, @Name, @Status, @StartTime, @FinishTime, @LogUrl, @now, @now)
    END
GO

