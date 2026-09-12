using ProposalEval.AgentHost;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = string.Join(" ", context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m)));
            return new Microsoft.AspNetCore.Mvc.ObjectResult(new
            {
                code = 400,
                data = (object?)null,
                error = string.IsNullOrWhiteSpace(message) ? "Invalid request." : message
            })
            {
                StatusCode = 400
            };
        };
    });

builder.Services.AddHttpClient<AppApiClient>(client =>
{
    client.BaseAddress = AppApiClient.ResolveBaseAddress();
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient("Llm", client =>
{
    client.Timeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddSingleton<ModelClientFactory>();
builder.Services.AddSingleton<McpProcessOptions>();
builder.Services.AddScoped<AgentRunService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Proposal evaluation agent host",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Agent host API v1");
});

app.MapControllers();
app.Run();
