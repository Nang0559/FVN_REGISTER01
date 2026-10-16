# Excel Platform normalization

## Canonical rule

All workbook mechanics are owned by the shared Excel platform. A module may only own a business adapter.

### Shared responsibilities

- workbook/file inspection
- sheet/header/range selection
- schema persistence and versioning
- field mapping
- preview and validation orchestration
- import batch/row/error persistence
- stage/commit lifecycle
- draft deletion
- export/template mechanics
- localized headers/resources

### Module responsibilities

- ModuleCode / EntityCode
- domain field definitions
- domain/business validation
- normalized-row to domain mapping
- existing domain service invocation

## Legacy removal

The following patterns are prohibited for new code and must be migrated from existing modules:

- F03EquipmentSchema / F03EquipmentFieldDefinition as Excel schema storage
- module-specific Excel import batch storage
- module-specific workbook readers/parsers
- module-specific schema lifecycle implementations
- module-specific preview/validation persistence

Before dropping a legacy table, the migration must:

1. Create the corresponding canonical schema.
2. Create the canonical schema version.
3. Copy every field mapping without losing source column, header, range or required/type metadata.
4. Map the legacy active version to `F03ExcelSchemas.CurrentVersionId`.
5. Validate row counts and active-schema counts.
6. Switch all application references to the canonical schema service.
7. Run Equipment and Endpoint Governance import/preview regression tests.
8. Only then drop the legacy tables and entities.

## Canonical module identifiers

- `EQUIPMENT / EQUIPMENT_MASTER`
- `ENDPOINT / ENDPOINT_GOVERNANCE`

Future Excel features must register a new ModuleCode/EntityCode pair instead of introducing another schema table.
