using Xunit;

// E2E journeys share one app process and one browser; run them serially for determinism.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
