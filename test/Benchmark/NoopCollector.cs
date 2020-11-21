using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark
{
    public class NoopCollector : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly CancellationTokenSource _listenerCts = new CancellationTokenSource();

        public NoopCollector(int port = 9080, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
            while (true)
            {
                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");

                try
                {
                    listener.Start();

                    // successfully listening
                    Port = port;
                    _listener = listener;

                    _listenerThread = new Thread(HandleHttpRequests);
                    _listenerThread.Start();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port++;
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        public int Port { get; }

        public void Dispose()
        {
            lock (_listener)
            {
                _listenerCts.Cancel();
                _listener.Stop();
            }
        }

        private void HandleHttpRequests()
        {
            var buffer = Encoding.UTF8.GetBytes("\"\"");

            while (true)
            {
                try
                {
                    var getCtxTask = Task.Run(() => _listener.GetContext());
                    getCtxTask.Wait(_listenerCts.Token);

                    var ctx = getCtxTask.Result;
                    using (var reader = new StreamReader(ctx.Request.InputStream))
                    {
                        reader.ReadToEnd();
                    }

                    ctx.Response.ContentType = "application/json";
                    ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    ctx.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is OperationCanceledException || ex is AggregateException)
                {
                    lock (_listener)
                    {
                        if (!_listener.IsListening)
                        {
                            return;
                        }
                    }

                    throw;
                }
            }
        }
    }
}
