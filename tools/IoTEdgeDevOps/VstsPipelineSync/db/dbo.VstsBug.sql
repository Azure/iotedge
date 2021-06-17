/****** Object:  Table [dbo].[VstsBuildRun]    Script Date: 2/12/2020 4:56:48 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsBug](
	[BuildId] [varchar](20) NOT NULL,
	[BugId] [varchar](20) NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsBug] PRIMARY KEY CLUSTERED 
(
	[BuildId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
