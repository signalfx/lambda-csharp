using System;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using SignalFx.LambdaWrapper;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SampleLambdaFunctions
{
    /// <summary>
    /// Simple AWS Lambda functions used to test the SignalFx wrappers.
    /// The actual AWS Lambda configuration just allows a single function but
    /// here more are defined to exercise all signatures of the wrapper.
    /// To select which function is actually run update the aws-lambda-tools-defaults.json
    /// file.
    /// </summary>
    public class Functions
    {
        // Static reference to the SignalFx function wrapper.
        private static FunctionWrapper s_functionWrapper = new FunctionWrapper();

        /// <summary>
        /// This is the modified handler to use the wrapper with a void synchronous function.
        /// </summary>
        public void VoidFunction(string input, ILambdaContext context)
        {
            s_functionWrapper.Invoke(OriginalVoidFunction, input, context);
        }

        /// <summary>
        /// Original void function.
        /// </summary>
        public void OriginalVoidFunction(string input, ILambdaContext context)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            input.ToUpper();
        }

        /// <summary>
        /// This is the modified handler to use the wrapper over a non-void synchronous function.
        /// </summary>
        public string FunctionWithReturn(string input, ILambdaContext context)
        {
            return s_functionWrapper.Invoke(OriginalFunctionWithReturn, input, context);
        }

        /// <summary>
        /// Original function that takes a string and does a ToUpper and returns it.
        /// </summary>
        public string OriginalFunctionWithReturn(string input, ILambdaContext context)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            return input.ToUpper();
        }

        /// <summary>
        /// This is the modified handler to use the wrapper with an asynchronous function.
        /// </summary>
        public async Task AsyncFunction(string input, ILambdaContext context)
        {
            await s_functionWrapper.InvokeAsync(OriginalAsyncFunction, input, context);
        }

        /// <summary>
        /// Original async function.
        /// </summary>
        public async Task OriginalAsyncFunction(string input, ILambdaContext context)
        {
            await Task.Run(() => OriginalVoidFunction(input, context));
        }

        /// <summary>
        /// This is the modified handler to use the wrapper with an asynchronous function
        /// that returns a result.
        /// </summary>
        public async Task<string> AsyncFunctionWithReturn(string input, ILambdaContext context)
        {
            return await s_functionWrapper.InvokeAsync(OriginalAsyncFunctionWithReturn, input, context);
        }

        /// <summary>
        /// Original async function.
        /// </summary>
        public async Task<string> OriginalAsyncFunctionWithReturn(string input, ILambdaContext context)
        {
            var result = await Task.Run(() => OriginalFunctionWithReturn(input, context));
            return result;
        }
    }
}
