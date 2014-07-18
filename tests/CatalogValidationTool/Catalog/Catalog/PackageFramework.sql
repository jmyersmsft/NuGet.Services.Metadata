


/****** Object:  Table [dbo].[PackageFrameworks]    Script Date: 2014-06-17 10:34:31 AM ******/
SET ANSI_NULLS ON;

SET QUOTED_IDENTIFIER ON;

CREATE TABLE [dbo].[PackageFrameworks](
	[Key] [int] IDENTITY(1,1) NOT NULL,
	[TargetFramework] [nvarchar](256) NULL,
	[Package_Key] [int] NOT NULL,
 CONSTRAINT [PK_PackageFrameworks] PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
);

ALTER TABLE [dbo].[PackageFrameworks]  WITH CHECK ADD  CONSTRAINT [FK_PackageFrameworks_Packages_Package_Key] FOREIGN KEY([Package_Key])
REFERENCES [dbo].[Packages] ([GalleryKey]);

ALTER TABLE [dbo].[PackageFrameworks] CHECK CONSTRAINT [FK_PackageFrameworks_Packages_Package_Key]

