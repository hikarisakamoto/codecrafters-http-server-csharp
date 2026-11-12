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

    // Handle this client without making new connections wait.
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

            var response = RouteRequest(requestLines, directory);
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
            // The client disconnected before sending all the headers.
            return null;
        }

        if (line.Length == 0)
        {
            // A blank line marks the end of the request headers.
            return requestLines;
        }

        requestLines.Add(line);
    }
}

static string RouteRequest(List<string> requestLines, string directory)
{
    if (requestLines.Count == 0)
    {
        return CreateResponse("400 Bad Request");
    }

    var requestLineParts = requestLines[0].Split(' ');
    if (requestLineParts.Length != 3)
    {
        return CreateResponse("400 Bad Request");
    }

    var target = requestLineParts[1];

    if (target == "/")
    {
        return CreateResponse("200 OK");
    }

    if (target.StartsWith("/files/", StringComparison.Ordinal))
    {
        return GetFileResponse(target, directory);
    }

    if (target.StartsWith("/echo/", StringComparison.Ordinal))
    {
        return CreateResponse("200 OK", body: target["/echo/".Length..]);
    }

    if (target == "/user-agent")
    {
        return CreateResponse("200 OK", body: GetHeaderValue(requestLines, "User-Agent"));
    }

    return CreateResponse("404 Not Found");
}

static string GetFileResponse(string target, string directory)
{
    var fileName = target["/files/".Length..];
    var path = Path.Combine(directory, fileName);
    Console.WriteLine($"File request: target='{target}', fileName='{fileName}', path='{path}'");

    if (!File.Exists(path))
    {
        Console.WriteLine($"File not found: '{path}'. Returning 404 Not Found.");
        return CreateResponse("404 Not Found");
    }

    Console.WriteLine($"Reading file: '{path}'");
    var body = File.ReadAllText(path);
    Console.WriteLine($"File read: '{path}', {Encoding.UTF8.GetByteCount(body)} response bytes. Returning 200 OK.");

    return CreateResponse("200 OK", "application/octet-stream", body);
}

static string GetHeaderValue(List<string> requestLines, string headerName)
{
    var prefix = headerName + ":";
    var header = requestLines.Skip(1).FirstOrDefault(line =>
        line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    return header?[prefix.Length..].Trim() ?? string.Empty;
}

static string CreateResponse(string status, string contentType = "text/plain", string body = "") =>
    $"HTTP/1.1 {status}\r\n" +
    $"Content-Type: {contentType}\r\n" +
    $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
    "Connection: close\r\n\r\n" +
    body;
