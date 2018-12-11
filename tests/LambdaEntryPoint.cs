using System;
using Microsoft.AspNetCore.Hosting;

namespace SignalFx.LambdaWrapper.Test
{
    public class LambdaEntryPoint : APIGatewayProxyFunctionWrapper
    {
        public LambdaEntryPoint()
        {
        
        }

        protected override void Init(IWebHostBuilder builder)
        {
            throw new NotImplementedException();
        }
    }
}
