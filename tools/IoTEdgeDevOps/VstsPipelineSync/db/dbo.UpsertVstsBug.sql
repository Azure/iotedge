CREATE PROCEDURE [dbo].[UpsertVstsBug]
    @Title varchar(200),
    @AreaPath varchar(200),
    @Priority varchar(20),
    @InProgress bit,
    @BugCount int
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsBug WHERE Title = @Title)
    BEGIN
        UPDATE dbo.VstsBug
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
        INSERT INTO dbo.VstsBug(Title, AreaPath, Priority, InProgress, BugCount, InsertedAt, UpdatedAt)
        VALUES (@Title, @AreaPath, @Priority, @InProgress, @BugCount, @now, @now)
    END
GO