using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using ProposalEval.Data;
using ProposalEval.Services;

var builder = WebApplication.CreateBuilder(args);

var sql = Environment.GetEnvironmentVariable("PROPOSAL_EVAL_SQL");
var useSql = !string.IsNullOrWhiteSpace(sql);

builder.Services.AddRazorPages();
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
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSql)
        options.UseSqlServer(sql);
    else
        options.UseInMemoryDatabase("ProposalEval");
});

builder.Services.AddSingleton(new AppStorageInfo { UsingSqlServer = useSql, FileStorageLabel = "Azure Blob" });
builder.Services.AddSingleton<IFileStore>(new BlobStorage(builder.Configuration));
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<RfqAppService>();
builder.Services.AddScoped<VendorAppService>();
builder.Services.AddScoped<JobAgentService>();
builder.Services.AddScoped<PreparedPromptService>();
builder.Services.AddSingleton<AgentJobQueue>();
builder.Services.AddHostedService<AgentDispatchWorker>();
builder.Services.AddHttpClient("AgentHost", client =>
{
    var raw = Environment.GetEnvironmentVariable("PROPOSAL_EVAL_AGENT_HOST")
              ?? builder.Configuration["AgentHost:BaseUrl"]
              ?? "http://localhost:5028";
    if (!raw.EndsWith('/'))
        raw += "/";
    client.BaseAddress = new Uri(raw);
    client.Timeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Proposal evaluation API",
        Version = "v1"
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Proposal evaluation API v1");
});

app.UseStaticFiles();
app.UseRouting();

app.MapGet("/files/{*key}", async (string key, IFileStore store, CancellationToken cancellationToken) =>
{
    var file = await store.GetAsync(BlobContainers.ProposalEvals, key, cancellationToken);
    return file is null
        ? Results.NotFound()
        : Results.File(file.Content, file.ContentType, file.FileName);
});

app.MapControllers();
app.MapRazorPages();
app.Run();
