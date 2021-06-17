CREATE PROCEDURE [dbo].[UpsertVstsBug]
    @BugId varchar(20),
    @BuildId varchar(20)
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsBug WHERE BugId = @BugId)
    BEGIN
        UPDATE dbo.VstsBug
        SET BugId = @BugId,
            BuildId = @BuildId,
            UpdatedAt = @now
        WHERE BugId = @BugId
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsBug(BugId, BuildId, InsertedAt, UpdatedAt)
        VALUES (@BuildId, @BugId, @now, @now)
    END
GO
