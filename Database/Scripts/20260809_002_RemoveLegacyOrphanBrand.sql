SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER SECURITY POLICY Security.TenantIsolationPolicy WITH (STATE = OFF);

IF
(
    SELECT COUNT(*)
    FROM dbo.Brands
    WHERE Id = '25a937d3-cee7-f011-bfa1-00155dd77908'
      AND CompanyId IS NULL
      AND Code = N'Code1'
      AND Name = N'test'
) <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51003, 'Expected exactly one matching orphaned test brand. Nothing was deleted.', 1;
END;

DELETE FROM dbo.Brands
WHERE Id = '25a937d3-cee7-f011-bfa1-00155dd77908'
  AND CompanyId IS NULL
  AND Code = N'Code1'
  AND Name = N'test';

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51004, 'Orphaned test brand deletion did not affect exactly one row.', 1;
END;

ALTER SECURITY POLICY Security.TenantIsolationPolicy WITH (STATE = ON);
COMMIT TRANSACTION;
