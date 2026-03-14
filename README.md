# EntityFrameworkCore.PolymorphicRelationships.PerformanceLab

Performance sandbox for `EntityFrameworkCore.PolymorphicRelationships`.

The project references the local package source directly and seeds three polymorphic owner types:

- `Post`
- `Blog`
- `Thread`

All three owner types can have `Comment` entities through the package's polymorphic relationship API.

## Run benchmarks

```bash
dotnet run -c Release --project EntityFrameworkCore.PolymorphicRelationships.PerformanceLab.csproj
```
