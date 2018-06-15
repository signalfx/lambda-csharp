using System;
using com.signalfuse.metrics.protobuf;

namespace signalfxlambdawrapper
{
    public class MetricSender
    {
		private static MetricWrapper singleton;
        
		public static void sendMetric(DataPoint datapoint)
        {
			if (singleton != null)
			{
				singleton.sendMetric(datapoint);
			}
        }

		protected internal static void setWrapper(MetricWrapper wrapper)
		{
			singleton = wrapper;			
		}
    }
}
