using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProposalEval.Mcp;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddHttpClient<ProposalEvalApiClient>(client =>
{
    client.BaseAddress = ApiBaseAddress.Resolve();
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddTransient<JobTools>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
