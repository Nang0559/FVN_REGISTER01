# Shared Excel Platform DI Rules

The shared Excel implementation is `FVN_REGISTER.Infrastructure.Services.Excel.ExcelPlatform` and its application-facing contract is `FVN_REGISTER.Application.Interfaces.Excel.IExcelPlatform`.

## Dependency rule

Business/application services MUST depend on `IExcelPlatform`, never on the concrete `ExcelPlatform` implementation.

Correct:

```csharp
private readonly IExcelPlatform _excelPlatform;

public SomeImportService(IExcelPlatform excelPlatform)
{
    _excelPlatform = excelPlatform;
}
```

Incorrect:

```csharp
private readonly ExcelPlatform _excelPlatform;
```

The composition root registers exactly one canonical mapping:

```csharp
services.AddScoped<IExcelPlatform, ExcelPlatform>();
```

Do not add a second registration merely to make a concrete dependency resolve. Doing so hides an architectural migration defect.

## Excel mechanics

NPOI/workbook mechanics belong only to `ExcelPlatform`. Equipment, Endpoint Governance, and other business modules must provide business mapping/adaptation only and must not instantiate workbook readers/writers directly.

## Validation

When changing Excel consumers, search for:

- `ExcelPlatform` in constructor parameters and fields
- `GetRequiredService<ExcelPlatform>` / `GetService<ExcelPlatform>`
- `new ExcelPlatform`
- direct NPOI workbook construction outside the shared Excel implementation

The application must be built with DI validation enabled in development/test so unresolved concrete dependencies are detected at startup.
