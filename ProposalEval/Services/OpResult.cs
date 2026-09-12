namespace ProposalEval.Services;

public sealed class OpResult<T>
{
    public bool Ok { get; private init; }
    public int Status { get; private init; }
    public T? Data { get; private init; }
    public string? Error { get; private init; }

    public static OpResult<T> Success(T data, int status = 200) =>
        new() { Ok = true, Status = status, Data = data };

    public static OpResult<T> Fail(int status, string error) =>
        new() { Ok = false, Status = status, Error = error };
}
