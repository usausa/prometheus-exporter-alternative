namespace HttpServer;

using System.Net;
using System.Text;

#pragma warning disable CA1848
internal class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;

    public Worker(ILogger<Worker> logger)
    {
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/");
        listener.Start();

        logger.LogInformation("Listening for HTTP requests...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var context = await listener.GetContextAsync().ConfigureAwait(false);
            logger.LogInformation("Received request: {RawUrl}", context.Request.RawUrl);

            var responseString = "Hello, world!";
            var buffer = Encoding.UTF8.GetBytes(responseString);

            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, stoppingToken).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }

        listener.Stop();
    }
}
