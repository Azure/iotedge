SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[VstsReleaseTask](
	[ReleaseDeploymentId] [int] NOT NULL,
	[Id] [int] NOT NULL,
	[Name] [varchar](100) NOT NULL,
	[Status] [varchar](20) NOT NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[FinishTime] [datetime2](7) NOT NULL,
	[LogUrl] [varchar](500) NULL,
	[InsertedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_VstsReleaseTask] PRIMARY KEY CLUSTERED 
(
	[ReleaseDeploymentId] ASC,
	[Id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[VstsReleaseTask]  WITH CHECK ADD  CONSTRAINT [FK_VstsReleaseTask_VstsReleaseDeployment] FOREIGN KEY([ReleaseDeploymentId])
REFERENCES [dbo].[VstsReleaseDeployment] ([Id])
GO

ALTER TABLE [dbo].[VstsReleaseTask] CHECK CONSTRAINT [FK_VstsReleaseTask_VstsReleaseDeployment]
GO

