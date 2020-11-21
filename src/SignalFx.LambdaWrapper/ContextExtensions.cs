using System;
using System.Collections.Generic;
using System.Reflection;

using Amazon.Lambda.Core;

namespace SignalFx.LambdaWrapper
{
    public static class ContextExtensions
    {
        private static readonly KeyValuePair<string, string> s_awsExecutionEnvTag = new KeyValuePair<string, string>(
            "aws_execution_env", Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV"));

        private static readonly KeyValuePair<string, string> s_wrapperVersion = new KeyValuePair<string, string>(
            "function_wrapper_version", "signalfx_lambda_" + Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString());

        public static List<KeyValuePair<string, string>> ExtractCommonTags(this ILambdaContext context)
        {
            var tags = new List<KeyValuePair<string, string>>(12);
            tags.Add(s_awsExecutionEnvTag);
            tags.Add(s_wrapperVersion);

            string functionArn = context.InvokedFunctionArn;
            string[] splitted = functionArn.Split(':');
            if ("lambda".Equals(splitted[2]))
            {
                // only add if its a lambda arn
                // formatting is per specification at http://docs.aws.amazon.com/general/latest/gr/aws-arns-and-namespaces.html#arn-syntax-lambda
                tags.Add(new KeyValuePair<string, string>("aws_function_name", context.FunctionName));
                tags.Add(new KeyValuePair<string, string>("aws_function_version", context.FunctionVersion));
                tags.Add(new KeyValuePair<string, string>("aws_region", splitted[3]));
                tags.Add(new KeyValuePair<string, string>("aws_account_id", splitted[4]));

                if ("function".Equals(splitted[5]))
                {
                    if (splitted.Length == 8)
                    {
                        tags.Add(new KeyValuePair<string, string>("aws_function_qualifier", splitted[7]));
                    }

                    string[] updatedArn = new string[8];
                    System.Array.Copy(splitted, 0, updatedArn, 0, splitted.Length);
                    updatedArn[7] = context.FunctionVersion;

                    tags.Add(new KeyValuePair<string, string>("lambda_arn", String.Join(":", updatedArn)));

                }
                else if ("event-source-mappings".Equals(splitted[5]) && splitted.Length > 6)
                {
                    tags.Add(new KeyValuePair<string, string>("event_source_mappings", splitted[6]));
                    tags.Add(new KeyValuePair<string, string>("lambda_arn", functionArn));
                }
            }

            return tags;
        }
    }
}
