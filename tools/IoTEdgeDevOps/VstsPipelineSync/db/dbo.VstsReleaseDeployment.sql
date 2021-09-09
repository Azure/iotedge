SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsReleaseDeployment](
	[Id] [int] NOT NULL,
	[ReleaseEnvironmentId] [int] NOT NULL,
	[Attempt] [int] NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsReleaseDeployment_1] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[VstsReleaseDeployment]  WITH CHECK ADD  CONSTRAINT [FK_VstsReleaseDeployment_VstsReleaseEnvironment] FOREIGN KEY([ReleaseEnvironmentId])
REFERENCES [dbo].[VstsReleaseEnvironment] ([Id])
GO

ALTER TABLE [dbo].[VstsReleaseDeployment] CHECK CONSTRAINT [FK_VstsReleaseDeployment_VstsReleaseEnvironment]
GO

