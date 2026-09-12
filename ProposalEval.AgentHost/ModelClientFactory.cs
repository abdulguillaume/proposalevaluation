using Microsoft.Extensions.AI;

namespace ProposalEval.AgentHost;

public sealed class ModelClientFactory(IConfiguration configuration, IHttpClientFactory http)
{
    public IChatClient Create()
    {
        var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrWhiteSpace(openRouterKey))
        {
            var openRouterBase = FirstNonEmpty(
                Environment.GetEnvironmentVariable("OPENROUTER_API_BASE"),
                configuration["OpenRouter:Endpoint"]) ?? "https://openrouter.ai/api/v1/";
            var openRouterModel = FirstNonEmpty(
                Environment.GetEnvironmentVariable("OPENROUTER_MODEL"),
                configuration["OpenRouter:Model"]) ?? "openai/gpt-4o-mini";

            return Wrap(new ChatCompletionsClient(
                http.CreateClient("Llm"),
                openRouterBase.TrimEnd('/') + "/chat/completions",
                openRouterModel,
                LlmAuth.Bearer,
                openRouterKey,
                "openrouter",
                new Dictionary<string, string>
                {
                    ["HTTP-Referer"] = "http://localhost:5028",
                    ["X-Title"] = "ProposalEval AgentHost"
                }));
        }

        var groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            var groqBase = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GROQ_API_BASE"),
                configuration["Groq:Endpoint"]) ?? "https://api.groq.com/openai/v1/";
            var groqModel = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GROQ_MODEL"),
                configuration["Groq:Model"]) ?? "openai/gpt-oss-120b";

            return Wrap(new ChatCompletionsClient(
                http.CreateClient("Llm"),
                groqBase.TrimEnd('/') + "/chat/completions",
                groqModel,
                LlmAuth.Bearer,
                groqKey,
                "groq"));
        }

        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                       ?? configuration["AzureOpenAI:ApiKey"];
        var azureEndpoint = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
            configuration["AzureOpenAI:Endpoint"]);
        var azureDeployment = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
            configuration["AzureOpenAI:Deployment"],
            configuration["AzureOpenAI:Model"]);
        var apiVersion = FirstNonEmpty(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION"),
            configuration["AzureOpenAI:ApiVersion"]) ?? "2024-06-01";

        if (string.IsNullOrWhiteSpace(azureKey))
            throw new InvalidOperationException("Set OPENROUTER_API_KEY (or GROQ_API_KEY / AZURE_OPENAI_API_KEY).");

        if (string.IsNullOrWhiteSpace(azureEndpoint) || string.IsNullOrWhiteSpace(azureDeployment))
            throw new InvalidOperationException(
                "AZURE_OPENAI_API_KEY is set. Also set AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT.");

        var azureUrl =
            $"{azureEndpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(azureDeployment)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";

        return Wrap(new ChatCompletionsClient(
            http.CreateClient("Llm"),
            azureUrl,
            azureDeployment,
            LlmAuth.AzureApiKey,
            azureKey,
            "azure-openai"));
    }

    private static IChatClient Wrap(IChatClient inner) =>
        inner.AsBuilder()
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = 30)
            .Build();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
