using FVN_REGISTER.EndpointAgent;
using Microsoft.Extensions.Hosting;

if (args.Length > 0 && string.Equals(args[0], "--protect-secret-stdin", StringComparison.OrdinalIgnoreCase))
{
    var secret = await Console.In.ReadToEndAsync();
    Console.WriteLine(WindowsSecretStore.Protect(secret));
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = "FVN Register Endpoint Agent");
builder.Services.Configure<EndpointAgentOptions>(builder.Configuration.GetSection("FVNEndpointAgent"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<EndpointCollector>();
builder.Services.AddHostedService<EndpointWorker>();

await builder.Build().RunAsync();
