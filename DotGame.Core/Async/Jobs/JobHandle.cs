namespace DotGame.Core.Async.Jobs;

public readonly struct JobHandle
{
    public static JobHandle Invalid => default;

    private readonly ulong token;

    public JobHandle(ulong token)
    {
        this.token = token;
    }

    public bool IsValid => token != 0;

    internal ulong Token => token;
}
