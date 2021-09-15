CREATE PROCEDURE [dbo].[UpsertVstsBugCounts]
    @Title varchar(200),
    @AreaPath varchar(200),
    @Priority varchar(20),
    @InProgress bit,
    @BugCount int
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsBugCounts WHERE Title = @Title)
    BEGIN
        UPDATE dbo.VstsBugCounts
        SET Title = @Title,
            AreaPath = @AreaPath,
            Priority = @Priority,
            InProgress = @InProgress,
            BugCount = @BugCount,
            UpdatedAt = @now
        WHERE Title = @Title
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsBugCounts(Title, AreaPath, Priority, InProgress, BugCount, InsertedAt, UpdatedAt)
        VALUES (@Title, @AreaPath, @Priority, @InProgress, @BugCount, @now, @now)
    END
GO