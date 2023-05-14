namespace TextReplace;

internal sealed record ReturnCode(int Value)
{
    public const int Success = 0;
    public const int Error = 1;

    public static implicit operator int(ReturnCode value) => value.Value;
}
