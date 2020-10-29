using System;
using System.Collections.Immutable;

namespace TestHelpers
{
    public static class BackendMock
    {
        public static IImmutableList<IMockSpan> CollectSpans(Action tracingAction, int expectedSpanCount = 1, int timeoutMilliseconds = 25000)
        {
            using (var collector = new MockZipkinCollector())
            {
                tracingAction();

                return collector.WaitForSpans(expectedSpanCount, timeoutMilliseconds);
            }
        }
    }
}
