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
		    BugCount = @BugCount
		WHERE QueryName = @QueryName
	END
	ELSE
	BEGIN
		INSERT INTO dbo.VstsBug(QueryName, BugCount)
		VALUES (@QueryName, @BugCount)
	END
