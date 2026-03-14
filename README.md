# EntityFrameworkCore.PolymorphicRelationships.PerformanceLab

Performance sandbox for `EntityFrameworkCore.PolymorphicRelationships` using PostgreSQL.

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

## Run a smoke test

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj -- --smoke
```
