SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsBuild](
	[BuildId] [varchar](20) NOT NULL,
	[BuildNumber] [varchar](20) NOT NULL,
	[DefinitionId] [int] NOT NULL,
	[DefinitionName] [varchar](100) NOT NULL,
	[SourceBranch] [varchar](100) NOT NULL,
	[SourceVersion] [varchar](100) NOT NULL,
	[SourceVersionDisplayUri] [varchar](500) NOT NULL,
	[WebUri] [varchar](500) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[Result] [varchar](20) NOT NULL,
	[QueueTime] [datetime2](7) NOT NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[FinishTime] [datetime2](7) NOT NULL,
	[WasScheduled] [varchar](20) NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsBuild] PRIMARY KEY CLUSTERED (
	 [BuildId] ASC
) WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

