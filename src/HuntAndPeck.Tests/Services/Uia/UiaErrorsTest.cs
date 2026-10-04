using System;
using System.IO;
using System.Runtime.InteropServices;
using HuntAndPeck.Services.Uia;
using Xunit;

namespace HuntAndPeck.Tests.Services.Uia
{
    public class UiaErrorsTest
    {
        [Fact]
        public void IsTargetFailure_TrueForVanishedOrHungTargets()
        {
            Assert.True(UiaErrors.IsTargetFailure(new COMException("gone", UiaErrors.ElementNotAvailable)));
            Assert.True(UiaErrors.IsTargetFailure(new TimeoutException()));
            Assert.True(UiaErrors.IsTargetFailure(new InvalidOperationException()));
            Assert.True(UiaErrors.IsTargetFailure(new InvalidCastException()));
        }

        [Fact]
        public void IsTargetFailure_FalseForOtherErrors()
        {
            Assert.False(UiaErrors.IsTargetFailure(new NullReferenceException()));
            Assert.False(UiaErrors.IsTargetFailure(new IOException()));
            Assert.False(UiaErrors.IsTargetFailure(new ArgumentException()));
        }

        [Fact]
        public void UiaTimeoutHResult_IsTargetFailureAndTimeout()
        {
            // COM interop surfaces UIA_E_TIMEOUT as a COMException carrying the HRESULT
            var ex = Marshal.GetExceptionForHR(UiaErrors.Timeout);

            Assert.True(UiaErrors.IsTargetFailure(ex));
            Assert.True(UiaErrors.IsTimeout(ex));
        }

        [Fact]
        public void IsTimeout_TrueForTimeoutExceptionOnly()
        {
            Assert.True(UiaErrors.IsTimeout(new TimeoutException()));
            Assert.False(UiaErrors.IsTimeout(new COMException("gone", UiaErrors.ElementNotAvailable)));
            Assert.False(UiaErrors.IsTimeout(new InvalidOperationException()));
        }

        [Fact]
        public void Describe_IncludesHResult()
        {
            var text = UiaErrors.Describe(new COMException("gone", UiaErrors.ElementNotAvailable));

            Assert.Equal("COMException 0x80040201: gone", text);
        }
    }
}
