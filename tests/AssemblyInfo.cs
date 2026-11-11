using Xunit;

// The PostgreSQL integration suite creates isolated databases per scenario.
// Bound test-class concurrency to the database service's actual connection
// capacity while retaining all tests and in-test concurrency assertions.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
