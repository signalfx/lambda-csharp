
using System.IO;

namespace SignalFx.LambdaWrapper.Helpers
{
    public interface IWebRequestor
    {
        Stream GetWriteStream();

        Stream Send();
    }
}
