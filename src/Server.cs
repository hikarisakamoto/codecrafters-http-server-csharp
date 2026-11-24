using System.Net;
using System.Net.Sockets;
using System.Text;

var directory = "/tmp";
var directoryFlagIndex = Array.IndexOf(args, "--directory");
if (directoryFlagIndex >= 0)
{
    if (directoryFlagIndex + 1 >= args.Length)
    {
        Console.Error.WriteLine("Error: --directory flag requires a path argument.");
        return;
    }

    directory = args[directoryFlagIndex + 1];
}

using var server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Server listening on port 4221...");

while (true)
{
    _ = HandleClientAsync(await server.AcceptTcpClientAsync(), directory);
}

static async Task HandleClientAsync(TcpClient client, string directory)
{
    try
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        while (await HttpRequest.ReadAsync(reader) is { } request)
        {
            var closeConnection = request.Header("Connection").Equals("close", StringComparison.OrdinalIgnoreCase);
            await stream.WriteAsync(Route(request, directory).ToBytes(closeConnection));
            if (closeConnection)
            {
                break;
            }
        }
    }
    catch (Exception exception) when (exception is IOException or SocketException)
    {
        Console.Error.WriteLine($"Client connection failed: {exception.Message}");
    }
}

static HttpResponse Route(HttpRequest request, string directory) => request.Target switch
{
    "" => new("400 Bad Request"),
    "/" => new("200 OK"),
    "/user-agent" => new("200 OK", request.Header("User-Agent")),
    var target when target.StartsWith("/echo/", StringComparison.Ordinal) =>
        new("200 OK", target["/echo/".Length..], Gzip: request.AcceptsGzip()),
    var target when target.StartsWith("/files/", StringComparison.Ordinal) =>
        HandleFile(request, Path.Combine(directory, target["/files/".Length..])),
    _ => new("404 Not Found"),
};

static HttpResponse HandleFile(HttpRequest request, string path)
{
    switch (request.Method)
    {
        case "GET" when File.Exists(path):
            return new("200 OK", File.ReadAllText(path), "application/octet-stream");
        case "POST":
            File.WriteAllText(path, request.Body);
            return new("201 Created");
        default:
            return new("404 Not Found");
    }
}
