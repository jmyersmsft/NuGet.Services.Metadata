USE [CatalogTest]
GO

/****** Object:  Table [dbo].[Packages]    Script Date: 6/17/2014 10:18:18 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Packages](
	[Key] [int] IDENTITY(1,1) NOT NULL,
	[PackageRegistrationKey] [int] NOT NULL,
	[Copyright] [nvarchar](max) NULL,
	[Created] [datetime] NOT NULL,
	[Description] [nvarchar](max) NULL,
	[DownloadCount] [int] NOT NULL DEFAULT ((0)),
	[ExternalPackageUrl] [nvarchar](max) NULL,
	[HashAlgorithm] [nvarchar](10) NULL,
	[Hash] [nvarchar](256) NOT NULL,
	[IconUrl] [nvarchar](max) NULL,
	[IsLatest] [bit] NOT NULL,
	[LastUpdated] [datetime] NOT NULL,
	[LicenseUrl] [nvarchar](max) NULL,
	[Published] [datetime] NOT NULL CONSTRAINT [DF_Published]  DEFAULT ('2011-12-05T21:22:06.4971292Z'),
	[PackageFileSize] [bigint] NOT NULL,
	[ProjectUrl] [nvarchar](max) NULL,
	[RequiresLicenseAcceptance] [bit] NOT NULL,
	[Summary] [nvarchar](max) NULL,
	[Tags] [nvarchar](max) NULL,
	[Title] [nvarchar](256) NULL,
	[Version] [nvarchar](64) NOT NULL,
	[FlattenedAuthors] [nvarchar](max) NULL,
	[FlattenedDependencies] [nvarchar](max) NULL,
	[IsLatestStable] [bit] NOT NULL DEFAULT ((0)),
	[Listed] [bit] NOT NULL DEFAULT ((0)),
	[IsPrerelease] [bit] NOT NULL DEFAULT ((0)),
	[ReleaseNotes] [nvarchar](max) NULL,
	[Language] [nvarchar](20) NULL,
	[MinClientVersion] [nvarchar](44) NULL,
	[UserKey] [int] NULL,
	[LastEdited] [datetime] NULL,
	[HideLicenseReport] [bit] NOT NULL,
	[LicenseNames] [nvarchar](max) NULL,
	[LicenseReportUrl] [nvarchar](max) NULL,
	[NormalizedVersion] [nvarchar](64) NULL,
PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)

GO

ALTER TABLE [dbo].[Packages]  WITH CHECK ADD  CONSTRAINT [FK_Packages_PackageRegistrations_PackageRegistrationKey] FOREIGN KEY([PackageRegistrationKey])
REFERENCES [dbo].[PackageRegistrations] ([Key])
GO

ALTER TABLE [dbo].[Packages] CHECK CONSTRAINT [FK_Packages_PackageRegistrations_PackageRegistrationKey]
GO

