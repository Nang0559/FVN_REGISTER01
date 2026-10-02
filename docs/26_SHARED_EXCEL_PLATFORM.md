# Shared Excel Platform

The canonical Excel implementation lives in the existing project boundaries:

- `FVN_REGISTER.Core/Excel` — domain definitions and lifecycle models.
- `FVN_REGISTER.Application/Interfaces/Excel` — application contract.
- `FVN_REGISTER.Infrastructure/Services/Excel` — NPOI workbook engine and persisted schema lifecycle.
- `FVN_REGISTER.API/Controllers/ExcelController.cs` — shared inspection/schema lifecycle API.
- `FVN_REGISTER.Web/Components/Excel/SharedExcelSchemaManager.razor` — reusable schema UI.
- `SQL/Excel/001_SharedExcelPlatform.sql` — shared schema/batch/error tables.

Do not add another module-specific workbook parser. Equipment and Endpoint Governance adapters must consume the shared engine. The persisted schema contains sheet/header/data range and exact source-column mappings, so an active schema reads the same columns that were selected when it was created.

Schema lifecycle: Draft -> Active -> Retired. An unused Draft can be soft-deleted; a schema with staged/committed import batches cannot be deleted.

Import lifecycle: Draft -> Uploaded -> Previewed -> Validated -> Staged -> Committed, with Failed/Cancelled/Deleted terminal states.

Endpoint Governance now uses the shared engine for workbook inspection and mapping instead of ClosedXML-specific workbook parsing. Equipment retains its existing business commit rules while its Excel parsing is being converged on the shared mapping contract.
