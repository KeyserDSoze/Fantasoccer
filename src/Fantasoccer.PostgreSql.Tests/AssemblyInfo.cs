using Xunit;

// Storage fixture tests inventory whole dedicated emulator accounts. Running
// their setup concurrently would make another fixture's documents part of a dump.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
