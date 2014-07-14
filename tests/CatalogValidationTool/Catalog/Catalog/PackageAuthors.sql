USE [CatalogTest] 
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[PackageAuthors](
	[Key] [int] IDENTITY(1,1) NOT NULL,
	[PackageKey] [int] NOT NULL,
	[Name] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)

GO

ALTER TABLE [dbo].[PackageAuthors]  WITH CHECK ADD  CONSTRAINT [FK_PackageAuthors_Packages_PackageKey] FOREIGN KEY([PackageKey])
REFERENCES [dbo].[Packages] ([GalleryKey])
GO

ALTER TABLE [dbo].[PackageAuthors] CHECK CONSTRAINT [FK_PackageAuthors_Packages_PackageKey]
GO