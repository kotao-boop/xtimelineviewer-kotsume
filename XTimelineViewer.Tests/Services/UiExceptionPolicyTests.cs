using System;
using System.Runtime.InteropServices;
using Xunit;
using XTimelineViewer.Services;

namespace XTimelineViewer.Tests.Services
{
    public class UiExceptionPolicyTests
    {
        [Fact]
        public void Cancellation_IsTheOnlySingleRecoverableException()
        {
            Assert.True(UiExceptionPolicy.CanContinue(new OperationCanceledException()));
            Assert.True(UiExceptionPolicy.CanContinue(new TaskCanceledException()));

            Assert.False(UiExceptionPolicy.CanContinue(new NullReferenceException()));
            Assert.False(UiExceptionPolicy.CanContinue(new InvalidOperationException()));
            Assert.False(UiExceptionPolicy.CanContinue(new COMException()));
            Assert.False(UiExceptionPolicy.CanContinue(new ObjectDisposedException("test")));
        }

        [Fact]
        public void Aggregate_IsRecoverableOnlyWhenEveryFailureIsCancellation()
        {
            Assert.True(UiExceptionPolicy.CanContinue(new AggregateException(
                new OperationCanceledException(),
                new TaskCanceledException())));

            Assert.False(UiExceptionPolicy.CanContinue(new AggregateException(
                new OperationCanceledException(),
                new InvalidOperationException())));
        }
    }
}

