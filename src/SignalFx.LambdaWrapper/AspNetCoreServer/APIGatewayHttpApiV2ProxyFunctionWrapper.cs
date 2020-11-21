using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.Core;

namespace SignalFx.LambdaWrapper.AspNetCoreServer
{
    /// <inheritdoc />
    /// <summary>
    /// APIGatewayHttpApiV2ProxyFunctionWrapper extends APIGatewayHttpApiV2ProxyFunction to provide monitoring functionality. Users should
    /// extend APIGatewayProxyFunctionWrapper and implement the Init method similar to Main function in the ASP.NET Core.
    /// The function handler for the Lambda function will point to the APIGatewayProxyFunctionWrapper class FunctionHandlerAsync
    /// method.
    /// </summary>
    public abstract class APIGatewayHttpApiV2ProxyFunctionWrapper : APIGatewayHttpApiV2ProxyFunction
    {
        private static readonly FunctionWrapper s_functionWrapper = new FunctionWrapper();

        /// <inheritdoc />
        /// <summary>
        /// This overriding method is what the Lambda function handler points to.
        /// </summary>
        public override async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandlerAsync(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext lambdaContext)
        {
            if (!TelemetryConfiguration.TracingEnabled && !TelemetryConfiguration.MetricsEnabled)
            {
                return await base.FunctionHandlerAsync(request, lambdaContext).ConfigureAwait(false);
            }

            return await s_functionWrapper.InvokeAPIGatewayHttpApiV2ProxyAsync(
                base.FunctionHandlerAsync,
                request,
                lambdaContext).ConfigureAwait(false);
        }
    }
}
