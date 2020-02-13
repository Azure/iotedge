﻿/****** Object:  Table [dbo].[VstsBuildRun]    Script Date: 2/12/2020 4:56:48 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsBuildRun](
	[BuildNumber] [varchar](20) NOT NULL,
	[DefinitionId] [int] NOT NULL,
	[SourceBranch] [varchar](100) NOT NULL,
	[SourceVersionDisplayUri] [varchar](500) NOT NULL,
	[WebUri] [varchar](500) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[Result] [varchar](20) NOT NULL,
	[QueueTime] [datetime2](7) NOT NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[FinishTime] [datetime2](7) NOT NULL,
	[CreatedTime] [datetime2](7) NOT NULL,
	[UpdateTime] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsBuildRun] PRIMARY KEY CLUSTERED 
(
	[BuildNumber] ASC,
	[DefinitionId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

