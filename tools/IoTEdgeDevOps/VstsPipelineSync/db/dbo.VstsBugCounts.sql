SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsBugCounts](
	[Title] [varchar](200) NOT NULL,
	[AreaPath] [varchar](200) NOT NULL,
	[Priority] [varchar](20) NOT NULL,
	[InProgress] [bit] NOT NULL,
	[BugCount] [int] NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsBugCount] PRIMARY KEY CLUSTERED 
(
	[Title] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO