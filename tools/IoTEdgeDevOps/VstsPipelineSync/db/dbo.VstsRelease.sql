SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsRelease](
	[Id] [int] NOT NULL,
	[Name] [varchar](100) NOT NULL,
	[SourceBranch] [varchar](100) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[WebUri] [varchar](500) NOT NULL,
	[DefinitionId] [int] NOT NULL,
	[DefinitionName] [varchar](100) NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsRelease] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

