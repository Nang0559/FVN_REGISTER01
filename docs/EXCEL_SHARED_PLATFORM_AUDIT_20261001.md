# Shared Excel Platform audit

This audit records the architectural invariant for the Excel migration on `feature/i18n-vi-ja`.

- Contract: `IExcelPlatform` in Application.
- Implementation: `ExcelPlatform` in Infrastructure.
- Canonical SQL metadata: `F03ExcelSchemas`, `F03ExcelSchemaVersions`, `F03ExcelSchemaFields`, `F03ExcelImportBatches`, `F03ExcelImportRows`, `F03ExcelImportErrors`.
- Business modules must not inject `ExcelPlatform` directly.
- Business modules must not create NPOI workbooks/readers/writers directly.
- DI must register `IExcelPlatform -> ExcelPlatform` once.

The existing startup failure for `EndpointGovernanceExcelImportService` is therefore a concrete-dependency migration defect, not a reason to register `ExcelPlatform` as an additional service.
