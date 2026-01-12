namespace Shared;

public static class JobTypes
{
    public const string HttpGet = "http_get";
    public const string Cpu = "cpu";
    public const string FileWrite = "file_write";

    public static readonly IReadOnlySet<string> BuiltIn = new HashSet<string>
    {
        HttpGet,
        Cpu,
        FileWrite
    };
}
