# EntityFrameworkCore.PolymorphicRelationships.PerformanceLab

Performance lab for `EntityFrameworkCore.PolymorphicRelationships` using PostgreSQL.

The lab is intended to produce reproducible benchmark evidence, not just ad-hoc local measurements.

It seeds three polymorphic owner types:

- `Post`
- `Blog`
- `Thread`

All three owner types can have `Comment` entities through the package's polymorphic relationship API.

Reference resolution works like this:

- if a local checkout of `EntityFrameworkCore.PolymorphicRelationships` exists next to this repo, the lab uses that project directly
- otherwise, the lab restores the pinned NuGet package version declared in `EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj`

## PostgreSQL configuration

The benchmark harness connects to PostgreSQL and creates a temporary database for each run.

Configuration is resolved in this order:

1. `POLYMORPHIC_PERF_POSTGRES`
2. `appsettings.json` using `ConnectionStrings:Postgres`

Example configuration file:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=your-password"
  }
}
```

`appsettings.example.json` is included in git. `appsettings.json` is intended for local use only and is ignored by git.

Safety rules:

- explicit PostgreSQL configuration is required; there is no built-in fallback connection string
- only localhost targets are allowed by default; set `POLYMORPHIC_PERF_ALLOW_REMOTE_HOST=true` only if you intentionally want to benchmark a remote instance
- the lab only creates and drops databases whose names start with `polymorphic_perf_`
- the configured PostgreSQL user still needs permission to create and drop databases

Environment override:

```bash
set POLYMORPHIC_PERF_POSTGRES=Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=your-password
```

## Run benchmarks

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj
```

Useful options:

- `--quick` runs a shorter BenchmarkDotNet job for local verification
- `--update-history` writes the generated summary back into `BenchmarkHistory/`
- `--history-label <label>` adds a stable suffix to the dated history markdown file
- any remaining arguments are forwarded to BenchmarkDotNet, for example `--filter "*LoadMorphMany*"`

Example quick validation run:

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj -- --quick --filter "*NonPolymorphic_Control_Post_Comments*"
```

Current methodology notes:

- benchmark sampling now uses evenly spaced owner/comment samples instead of the first `N` rows
- mixed-owner benchmarks now draw from `Post`, `Blog`, and `Thread` comment sets explicitly
- no-tracking benchmarks use dedicated no-tracking `DbContext` options end-to-end
- projection benchmarks explicitly opt into the package's experimental `Select(...)` projection support
- each benchmark invocation writes run metadata with git SHAs, package source/version, benchmark args, and detected PostgreSQL version into `BenchmarkDotNet.Artifacts/run-metadata/`
- each benchmark invocation also writes machine-readable and markdown summaries into `BenchmarkDotNet.Artifacts/summaries/`

## Benchmark history

Committed benchmark summaries live in `BenchmarkHistory/` to track performance changes over time.

Current notes:

- native `Select(...)` projection benchmarks opt into the package's experimental projection support
- only runs generated with the current methodology and committed intentionally via `--update-history` should be treated as publishable history

## Run a smoke test

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj -- --smoke
```

## CI

The repo CI builds the lab, runs the smoke test against PostgreSQL, and runs a quick single-benchmark validation to ensure summary and metadata output continue to work.
