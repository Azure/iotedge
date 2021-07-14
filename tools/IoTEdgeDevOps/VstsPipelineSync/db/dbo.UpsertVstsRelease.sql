SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[UpsertVstsRelease]
    @Id int,
    @Name varchar(100),
    @SourceBranch varchar(100),
    @Status varchar(20),
    @WebUri varchar(500),
    @DefinitionId int,
    @DefinitionName varchar(100)
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsRelease WHERE [Id] = @Id)
    BEGIN
        UPDATE dbo.VstsRelease
        SET [Name] = @Name,
            [SourceBranch] = @SourceBranch,
            [Status] = @Status,
            WebUri = @WebUri,
            DefinitionId = @DefinitionId,
            DefinitionName = @DefinitionName,
            UpdatedAt = @now
        WHERE [Id] = @Id
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsRelease(Id, [Name], [SourceBranch], [Status], WebUri, DefinitionId, DefinitionName, InsertedAt, UpdatedAt)
        VALUES (@Id, @Name, @SourceBranch, @Status, @WebUri, @DefinitionId, @DefinitionName, @now, @now)
    END
GO
