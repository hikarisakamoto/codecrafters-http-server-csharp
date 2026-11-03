using System.Net;
using System.Net.Sockets;
using System.Text;

using var server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Server listening on port 4221...");

while (true)
{
    var client = await server.AcceptTcpClientAsync();

    _ = HandleClientAsync(client);
}

static async Task HandleClientAsync(TcpClient client)
{
    using (client)
    {
        try
        {
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            var requestLines = new List<string>();

            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    return;
                }

                if (line.Length == 0)
                {
                    break;
                }

                requestLines.Add(line);
            }

            var response = GetResponse(requestLines);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            Console.Error.WriteLine($"Client connection failed: {exception.Message}");
        }
    }
}

static string GetResponse(List<string> requestLines)
{
    if (requestLines.Count == 0)
    {
        return CreateResponse("400 Bad Request");
    }

    var requestParts = requestLines[0].Split(' ');
    if (requestParts.Length != 3)
    {
        return CreateResponse("400 Bad Request");
    }

    var target = requestParts[1];
    if (target == "/")
    {
        return CreateResponse("200 OK");
    }

    if (target.StartsWith("/echo/", StringComparison.Ordinal))
    {
        return CreateResponse("200 OK", target["/echo/".Length..]);
    }

    if (target == "/user-agent")
    {
        const string headerName = "User-Agent:";
        var header = requestLines.Skip(1).FirstOrDefault(line =>
            line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase));
        var userAgent = header?[headerName.Length..].Trim() ?? string.Empty;
        return CreateResponse("200 OK", userAgent);
    }

    return CreateResponse("404 Not Found");
}

static string CreateResponse(string status, string body = "") =>
    $"HTTP/1.1 {status}\r\n" +
    "Content-Type: text/plain\r\n" +
    $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
    "Connection: close\r\n\r\n" +
    body;
