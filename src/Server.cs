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
    var client = await server.AcceptTcpClientAsync();

    _ = HandleClientAsync(client, directory);
}

static async Task HandleClientAsync(TcpClient client, string directory)
{
    using (client)
    {
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var requestLines = await ReadRequestAsync(reader);
            if (requestLines is null)
            {
                return;
            }

            var response = GetResponse(requestLines, directory);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            Console.Error.WriteLine($"Client connection failed: {exception.Message}");
        }
    }
}

static async Task<List<string>?> ReadRequestAsync(StreamReader reader)
{
    var requestLines = new List<string>();

    while (true)
    {
        var line = await reader.ReadLineAsync();
        if (line is null)
        {
            return null;
        }

        if (line.Length == 0)
        {
            return requestLines;
        }

        requestLines.Add(line);
    }
}

static string GetResponse(List<string> requestLines, string directory)
{
    if (requestLines.Count == 0)
    {
        return CreateResponse("400 Bad Request", "text/plain");
    }

    var requestParts = requestLines[0].Split(' ');
    if (requestParts.Length != 3)
    {
        return CreateResponse("400 Bad Request", "text/plain");
    }

    var target = requestParts[1];
    if (target == "/")
    {
        return CreateResponse("200 OK", "text/plain");
    }

    if (target.StartsWith("/files/", StringComparison.Ordinal))
    {
        return GetFileResponse(target, directory);
    }

    if (target.StartsWith("/echo/", StringComparison.Ordinal))
    {
        return CreateResponse("200 OK", "text/plain", target["/echo/".Length..]);
    }

    if (target == "/user-agent")
    {
        const string headerName = "User-Agent:";
        var header = requestLines.Skip(1).FirstOrDefault(line =>
            line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase));
        var userAgent = header?[headerName.Length..].Trim() ?? string.Empty;
        return CreateResponse("200 OK", "text/plain", userAgent);
    }

    return CreateResponse("404 Not Found", "text/plain");
}

static string GetFileResponse(string target, string directory)
{
    var fileName = target["/files/".Length..];
    var path = Path.Combine(directory, fileName);
    Console.WriteLine($"File request: target='{target}', fileName='{fileName}', path='{path}'");
    if (!File.Exists(path))
    {
        Console.WriteLine($"File not found: '{path}'. Returning 404 Not Found.");
        return CreateResponse("404 Not Found", "text/plain");
    }

    Console.WriteLine($"Reading file: '{path}'");
    var body = File.ReadAllText(path);
    Console.WriteLine($"File read: '{path}', {Encoding.UTF8.GetByteCount(body)} response bytes. Returning 200 OK.");
    return CreateResponse("200 OK", "application/octet-stream", body);
}

static string CreateResponse(string status, string contentType, string body = "") =>
    $"HTTP/1.1 {status}\r\n" +
    $"Content-Type: {contentType}\r\n" +
    $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
    "Connection: close\r\n\r\n" +
    body;
