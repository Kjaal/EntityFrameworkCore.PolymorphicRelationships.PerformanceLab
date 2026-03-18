# Benchmark History - 2026-03-14

Advisory: this snapshot predates the current reproducibility, mixed-sampling, and no-tracking methodology refresh. Regenerate before using it as public proof.

Dataset:

- `OwnerCountPerType = 1000`
- `CommentsPerOwner = 20`
- PostgreSQL benchmark backend

## Summary

| Method | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| `NonPolymorphic_Control_Post_Comments` | 27.131 ms | 767.43 KB | 1.00 |
| `Manual_Post_Comment_Filter` | 346.432 ms | 884.85 KB | 12.79 |
| `NonPolymorphic_Control_IncludeComments_For_Posts_Batch` | 9.129 ms | 2622.01 KB | 0.34 |
| `NonPolymorphic_Control_IncludeComments_For_Posts_Batch_NoTracking` | 4.480 ms | 1321.55 KB | 0.17 |
| `Extension_LoadMorphMany_For_Posts` | 68.361 ms | 4563.7 KB | 2.52 |
| `Extension_LoadMorphMany_For_Posts_Batch` | 8.054 ms | 3580.65 KB | 0.30 |
| `Extension_LoadMorphMany_For_Posts_Batch_NoTracking` | 6.560 ms | 2078.94 KB | 0.24 |
| `NonPolymorphic_Control_LoadLatestComment_For_Posts` | 200.977 ms | 996.04 KB | 7.42 |
| `Extension_LoadMorphLatestOfMany_For_Posts` | 66.819 ms | 2817.09 KB | 2.47 |
| `Extension_LoadMorphMany_For_Blogs_And_Threads` | 123.941 ms | 8889.26 KB | 4.58 |
| `Extension_LoadMorphManyAcross_For_Blogs_And_Threads_Batch` | 15.840 ms | 6961.53 KB | 0.58 |
| `Extension_LoadMorphManyAcross_For_Blogs_And_Threads_Batch_NoTracking` | 12.402 ms | 3925.44 KB | 0.46 |
| `Extension_LoadMixedMorphOwners_Batch` | 7.024 ms | 2476.33 KB | 0.26 |
| `NonPolymorphic_Control_LoadTags_For_Posts_Batch` | 4.187 ms | 1617.98 KB | 0.15 |
| `Extension_LoadMorphToMany_For_Posts_Batch` | 3.674 ms | 2375.79 KB | 0.14 |
| `Extension_LoadMorphToMany_For_Posts_Batch_NoTracking` | 3.678 ms | 2322.17 KB | 0.14 |
| `NonPolymorphic_Control_LoadCommentOwners_WithInclude` | 3.349 ms | 1528.48 KB | 0.12 |
| `NonPolymorphic_Control_LoadCommentOwners_WithInclude_NoTracking` | 3.167 ms | 1568.92 KB | 0.12 |
| `Extension_LoadMixedMorphOwners_Batch_WithPlans` | 7.448 ms | 2538.74 KB | 0.27 |
| `Extension_LoadMixedMorphOwners_Batch_WithPlans_NoTracking` | 7.366 ms | 2477.62 KB | 0.27 |

## Highlights

- Batch morph loading is the strongest package scenario.
- Mixed-principal inverse loading performs well once batched across principal types.
- No-tracking helps most on batch collection loads and mixed-principal batch loads.
- `latestOfMany` is substantially faster than the per-owner control loop, but still allocates more.
- `morphToMany` batch loading is now competitive with the non-polymorphic control path.

## Notes

- `Extension_LoadMorphMany_For_Blogs_And_Threads` reported a bimodal distribution.
- Several benchmarks had removed outliers; see BenchmarkDotNet artifacts for raw details.
