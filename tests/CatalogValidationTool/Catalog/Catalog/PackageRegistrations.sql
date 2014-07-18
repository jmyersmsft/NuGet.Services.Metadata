


/****** Object:  Table [dbo].[PackageRegistrations]    Script Date: 2014-06-18 11:30:25 AM ******/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

CREATE TABLE [dbo].[PackageRegistrations](
	[Key] [int] IDENTITY(1,1) NOT NULL,
	[Id] [nvarchar](128) NOT NULL UNIQUE,
	[DownloadCount] [int] NOT NULL DEFAULT ((0)),
PRIMARY KEY CLUSTERED 
(
	[Key] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)


