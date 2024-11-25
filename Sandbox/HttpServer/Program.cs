using HttpServer;

var builder = Host.CreateApplicationBuilder(args);

var setting = builder.Configuration.GetSection("Server").Get<WorkerOptions>()!;
builder.Services.AddSingleton(setting);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
