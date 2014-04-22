DECLARE		@ProcessedDateTime datetime = GETUTCDATE()

UPDATE		LogPackages
SET			ProcessedDateTime = @ProcessedDateTime
WHERE		[Key] IN @packageAssertionKeys

UPDATE		LogPackageOwners
SET			ProcessedDateTime = @ProcessedDateTime
WHERE		[Key] IN @packageOwnerAssertionKeys