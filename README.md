# EntityFrameworkCore.PolymorphicRelationships.PerformanceLab

Performance sandbox for `EntityFrameworkCore.PolymorphicRelationships` using PostgreSQL.

The lab is intended to validate relative performance trends while the package evolves. It is not yet a final public benchmark suite.

The project references the local package source directly and seeds three polymorphic owner types:

- `Post`
- `Blog`
- `Thread`

All three owner types can have `Comment` entities through the package's polymorphic relationship API.

## PostgreSQL configuration

The benchmark harness connects to PostgreSQL and creates a temporary database for each run.

Configuration is resolved in this order:

1. `POLYMORPHIC_PERF_POSTGRES`
2. `appsettings.json` using `ConnectionStrings:Postgres`
3. built-in fallback connection string

Example configuration file:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=your-password"
  }
}
```

`appsettings.example.json` is included in git. `appsettings.json` is intended for local use only.

Fallback connection string:

```text
Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres
```

Environment override:

```bash
set POLYMORPHIC_PERF_POSTGRES=Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=your-password
```

The configured user must have permission to create and drop databases.

## Run benchmarks

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj
```

Current methodology notes:

- benchmark sampling now uses evenly spaced owner/comment samples instead of the first `N` rows
- mixed-owner benchmarks now draw from `Post`, `Blog`, and `Thread` comment sets explicitly
- no-tracking benchmarks use dedicated no-tracking `DbContext` options end-to-end
- projection benchmarks explicitly opt into the package's experimental `Select(...)` projection support

## Benchmark history

Committed benchmark summaries live in `BenchmarkHistory/` to track performance changes over time.

Current notes:

- native `Select(...)` projection benchmarks opt into the package's experimental projection support
- benchmark history should be treated as advisory until the scenario matrix and reporting pipeline are fully stabilized

## Run a smoke test

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj -- --smoke
```
