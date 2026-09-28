using FVN_REGISTER.EndpointAgent;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "FVN Register Endpoint Agent");
builder.Services.Configure<EndpointAgentOptions>(builder.Configuration.GetSection("FVNEndpointAgent"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<EndpointCollector>();
builder.Services.AddHostedService<EndpointWorker>();

await builder.Build().RunAsync();
