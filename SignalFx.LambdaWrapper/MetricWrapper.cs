using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using com.signalfuse.metrics.protobuf;

namespace SignalFx.LambdaWrapper
{
    public class MetricWrapper : IDisposable
    {
        // signalfx env variables
        private static string AUTH_TOKEN = "SIGNALFX_AUTH_TOKEN";
        private static string TIMEOUT_MS = "SIGNALFX_SEND_TIMEOUT";
        

        //metric names
        protected static string METRIC_NAME_PREFIX = "function.";
        protected static string METRIC_NAME_INVOCATIONS = "invocations";
        protected static string METRIC_NAME_COLD_STARTS = "cold_starts";
        protected static string METRIC_NAME_ERRORS = "errors";
        protected static string METRIC_NAME_DURATION = "duration";
                

        //dimension names
        protected static string FUNCTION_NAME = "aws_function_name";
        protected static string FUNCTION_VERSION = "aws_function_version";
        protected static string METRIC_SOURCE = "aws_function_wrapper";
        protected static string REGION_DIMENSION = "aws_region";
        protected static string ACCOUNT_ID_DIMENSION = "aws_account_id";
        //TODO: Find appropriate home for the shared field WrapperVersion
        protected static string WRAPPER_VERSION = AspNetCoreServer.Extensions.WrapperVersion;
        
        private readonly System.Diagnostics.Stopwatch watch;
        private readonly IDictionary<string, string> defaultDimensions;
        private readonly ISignalFxReporter reporter;
        private static bool isColdStart = true;


        public MetricWrapper(ILambdaContext context) : this(context, null)
        {
            
        }

        public MetricWrapper(ILambdaContext context, 
                             List<Dimension> dimensions) : this(context, dimensions, GetEnvironmentVariable(AUTH_TOKEN))
        {
            
        }

        public MetricWrapper(ILambdaContext context,
                             List<Dimension> dimensions,
                             String authToken)
        {
            int timeoutMs = 300; //default timeout 300ms
            if (int.TryParse(GetEnvironmentVariable(TIMEOUT_MS), out int intValue))
            {
                timeoutMs = intValue;
            }

            // create endpoint
            SignalFxLambdaEndpoint signalFxEndpoint = new SignalFxLambdaEndpoint();


            // create reporter with endpoint
            reporter = new SignalFxReporter(signalFxEndpoint.ToString(), authToken, timeoutMs);

            // get default dimensions
            defaultDimensions = GetDefaultDimensions(context);
         
            // set wrapper singleton context
            MetricSender.setWrapper(this);

            
            watch = System.Diagnostics.Stopwatch.StartNew();
            sendMetricCounter(METRIC_NAME_INVOCATIONS, MetricType.COUNTER);
            if (isColdStart)
            {
              isColdStart = false;
              sendMetricCounter(METRIC_NAME_COLD_STARTS, MetricType.COUNTER);
            }

        }

        private void sendMetricCounter(string metricName, MetricType metricType)
        {
            Datum datum = new Datum();
            datum.intValue = 1;
            
            sendMetric(metricName, metricType, datum);
        }


        private void sendMetric(string metricName, MetricType metricType, 
                               Datum datum)
        {
            DataPoint dp = new DataPoint();
            dp.metric = METRIC_NAME_PREFIX + metricName;
            dp.metricType = metricType;
            dp.value = datum;
            
            MetricSender.sendMetric(dp);
        }

        
        protected internal void sendMetric(DataPoint dp)
        {
            // send the metric
            AddDimensions(dp, defaultDimensions);
            DataPointUploadMessage msg = new DataPointUploadMessage();
            msg.datapoints.Add(dp);
            reporter.Send(msg);
        }
  
        public void Dispose()
        {
            //end stopwatch and send duration
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            Datum timer = new Datum();
            timer.doubleValue = elapsedMs;
            sendMetric(METRIC_NAME_DURATION, MetricType.GAUGE, timer);
        }

        public void Error()
        {
            sendMetricCounter(METRIC_NAME_ERRORS, MetricType.COUNTER);
        }

        
        
        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        private static Dictionary<string, string> GetDefaultDimensions(ILambdaContext context) 
        {
            Dictionary<string, string> defaultDimensions = new Dictionary<string, string>();
            string functionArn = context.InvokedFunctionArn;
            string[] splitted = functionArn.Split(':');
            if ("lambda".Equals(splitted[2])) 
            {
                // only add if its a lambda arn
                // formatting is per specification at http://docs.aws.amazon.com/general/latest/gr/aws-arns-and-namespaces.html#arn-syntax-lambda
                defaultDimensions.Add("aws_function_name", context.FunctionName);
                defaultDimensions.Add("aws_function_version", context.FunctionVersion);
                defaultDimensions.Add("aws_region", splitted[3]);
                defaultDimensions.Add("aws_account_id", splitted[4]);
                
                if ("function".Equals(splitted[5]))
                {
                    if (splitted.Length == 8)
                    {
                        defaultDimensions.Add("aws_function_qualifier", splitted[7]);
                    }
                    
                    string[] updatedArn = new string[8];
                    System.Array.Copy(splitted, 0, updatedArn, 0, splitted.Length);
                    updatedArn[7] = context.FunctionVersion;

                    defaultDimensions.Add("lambda_arn", String.Join(":", updatedArn));
                   
                }
                else if ("event-source-mappings".Equals(splitted[5]) && splitted.Length > 6)
                {
                    defaultDimensions.Add("event_source_mappings", splitted[6]);
                    defaultDimensions.Add("lambda_arn", functionArn);
                }
            }
            string runTimeEnv = System.Environment.GetEnvironmentVariable("AWS_EXECUTION_ENVIRONMENT");
            if (!string.IsNullOrEmpty(runTimeEnv))
            {
                defaultDimensions.Add("aws_execution_env", runTimeEnv);
            }
            string wrapperVersion = getWrapperVersionString();
            if (!string.IsNullOrEmpty(wrapperVersion))
            {
                defaultDimensions.Add("function_wrapper_version", wrapperVersion);
            }

            defaultDimensions.Add("metric_source", "lambda_wrapper");
           
         
            return defaultDimensions;
        }
        
        private static String getWrapperVersionString() 
        {
            return "signalfx-lambda-"+WRAPPER_VERSION;
        }


        protected virtual void AddDimensions(DataPoint dataPoint, IDictionary<string, string> dimensions)
        {
            foreach (KeyValuePair<string, string> entry in dimensions)
            {
                if (!string.IsNullOrEmpty(entry.Value))
                {
                    AddDimension(dataPoint, entry.Key, entry.Value);
                }
            }
        }

        protected virtual void AddDimension(DataPoint dataPoint, string key, string value)
        {
            Dimension dimension = new Dimension();
            dimension.key = key;
            dimension.value = value;
            dataPoint.dimensions.Add(dimension);
        }
    }
}
