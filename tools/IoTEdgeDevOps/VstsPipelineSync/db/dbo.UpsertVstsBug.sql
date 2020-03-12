CREATE PROCEDURE [dbo].[UpsertVstsBug]
    @QueryName varchar(20),
    @BugCount int
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsBug WHERE QueryName = @QueryName)
    BEGIN
        UPDATE dbo.VstsBug
        SET QueryName = @QueryName,
            BugCount = @BugCount,
            UpdatedAt = @now
        WHERE QueryName = @QueryName
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsBug(QueryName, BugCount, InsertedAt, UpdatedAt)
        VALUES (@QueryName, @BugCount, @now, @now)
    END
GO
