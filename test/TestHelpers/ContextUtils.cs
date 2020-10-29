using System.IO;

using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Newtonsoft.Json;

namespace TestHelpers
{
    public static class ContextUtils
    {
        public static ILambdaContext FromJsonFile(string jsonContextFile)
        {
            var contextStr = File.ReadAllText(jsonContextFile);
            return JsonConvert.DeserializeObject<TestLambdaContext>(contextStr);
        }
    }
}
