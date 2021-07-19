SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsReleaseEnvironment](
    [Id] [int] NOT NULL,
	[ReleaseId] [int] NOT NULL,
	[DefinitionId] [int] NOT NULL,
	[DefinitionName] [varchar](100) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsReleaseEnvrionment] PRIMARY KEY CLUSTERED 
(
    [Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[VstsReleaseEnvironment]  WITH CHECK ADD  CONSTRAINT [FK_VstsReleaseEnvironment_RelaseId] FOREIGN KEY([ReleaseId])
REFERENCES [dbo].[VstsRelease] ([id])
GO

ALTER TABLE [dbo].[VstsReleaseEnvironment] CHECK CONSTRAINT [FK_VstsReleaseEnvironment_RelaseId]
GO