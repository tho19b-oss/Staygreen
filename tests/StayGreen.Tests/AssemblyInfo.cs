using Xunit;

// Loc.Language ist globaler Zustand; die Tests sind klein und laufen deshalb nacheinander.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
