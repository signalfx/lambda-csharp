
namespace SignalFx.LambdaWrapper.Helpers
{
    /// <summary>
    /// A factory for creating IWebRequestor instances
    /// </summary>
    public interface IWebRequestorFactory
    {
        IWebRequestor GetRequestor();
    }
}
