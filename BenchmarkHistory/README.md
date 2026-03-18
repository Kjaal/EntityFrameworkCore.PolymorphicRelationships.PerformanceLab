# Benchmark History Notes

The committed benchmark summaries in this folder are historical snapshots, not a guaranteed source of the latest methodology.

Before using any summary as public evidence:

- confirm the run metadata in `BenchmarkDotNet.Artifacts/run-metadata/`
- confirm the generated summary in `BenchmarkDotNet.Artifacts/summaries/`
- confirm the benchmark categories and filters used for the run
- verify the current sampling, no-tracking, and projection settings in `PolymorphicRelationshipBenchmarks.cs`
- regenerate summaries after any benchmark methodology change using `--update-history`

`latest-summary.json` should only be updated from a full benchmark run that also produced matching run metadata and artifact summaries.
