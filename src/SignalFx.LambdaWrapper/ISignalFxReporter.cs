using com.signalfuse.metrics.protobuf;

namespace SignalFx.LambdaWrapper
{
    /// <summary>
    /// Base interface for the reporter that actually transmits reporting data
    /// </summary>
    public interface ISignalFxReporter
    {
        /// <summary>
        /// Report the upload message
        /// </summary>
        /// <param name="msg">The message to report</param>
        void Send(DataPointUploadMessage msg);
    }
}
