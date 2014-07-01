USE [CatalogTest]
GO

/****** Object:  Table [dbo].[PackageDependencies]    Script Date: 2014-06-17 10:36:10 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[PackageDependencies](
	[Key] [int] IDENTITY(1,1) NOT NULL,
	[PackageKey] [int] NOT NULL,
	[Id] [nvarchar](128) NULL,
	[VersionSpec] [nvarchar](256) NULL,
	[TargetFramework] [nvarchar](256) NULL,
PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)

GO

ALTER TABLE [dbo].[PackageDependencies]  WITH CHECK ADD  CONSTRAINT [FK_PackageDependencies_Packages_PackageKey] FOREIGN KEY([PackageKey])
REFERENCES [dbo].[Packages] ([Key])
GO

ALTER TABLE [dbo].[PackageDependencies] CHECK CONSTRAINT [FK_PackageDependencies_Packages_PackageKey]
GO
