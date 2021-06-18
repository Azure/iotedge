CREATE PROCEDURE [dbo].[UpsertVstsBug]
    @BuildId varchar(20),
    @BugId varchar(20)
AS
    DECLARE @now datetime2;
    SET @now = SYSDATETIME();

    IF EXISTS (SELECT 1 FROM dbo.VstsBug WHERE BuildId = @BuildId)
    BEGIN
        UPDATE dbo.VstsBug
        SET BuildId = @BuildId,
            BugId = @BugId,
            UpdatedAt = @now
        WHERE BugId = @BugId
    END
    ELSE
    BEGIN
        INSERT INTO dbo.VstsBug(BuildId, BugId, InsertedAt, UpdatedAt)
        VALUES (@BuildId, @BugId, @now, @now)
    END
GO
