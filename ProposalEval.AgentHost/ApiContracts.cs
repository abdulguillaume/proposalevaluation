using Microsoft.AspNetCore.Mvc;

namespace ProposalEval.AgentHost;

public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult Success(int code, object? data) =>
        StatusCode(code, new { code, data });

    protected ObjectResult Fail(int code, string error, object? data = null) =>
        StatusCode(code, new { code, data, error });
}
