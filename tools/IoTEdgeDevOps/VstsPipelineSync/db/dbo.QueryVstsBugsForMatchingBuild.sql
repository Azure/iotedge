CREATE PROCEDURE [dbo].[QueryVstsBugsForMatchingBuild]
    @BuildId varchar(20)
AS
    SELECT 1 FROM dbo.VstsBug WHERE BuildId = @BuildId
GO

