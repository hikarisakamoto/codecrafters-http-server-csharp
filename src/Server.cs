using System.Net;
using System.Net.Sockets;
using System.Text;

const string eol = "\r\n";
const string eoh = "\r\n\r\n";
const string httpOkResponse = "HTTP/1.1 200 OK";

TcpListener server = new(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Server started, waiting for client...");
while (true)
{
    using var client = await server.AcceptTcpClientAsync(); // wait for client
    await using var stream = client.GetStream();
    var buffer = new byte[1_024];
    var bytesRead = await stream.ReadAsync(buffer);
    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    var requestLines = request.Split("\r\n");
    var requestLine = requestLines[0];
    var requestParts = requestLine.Split(' ');
    var response = string.Empty;
    if (requestParts.Length < 3)
    {
        response = "HTTP/1.1 400 Bad Request\r\n\r\n";
        await WriteResponseAsync(stream, response);
        continue;
    }

    var target = requestParts[1];
    Console.WriteLine($"Processing request for target: {target}");
    if (target == "/")
    {
        response = $"{httpOkResponse}{eoh}";
        await WriteResponseAsync(stream, response);
        continue;
    }

    if (target.StartsWith("/echo/"))
    {
        var echoMessage = target["/echo/".Length..];
        Console.WriteLine($"Echoing message: {echoMessage}");
        response = $"{httpOkResponse}{eol}Content-Type: text/plain{eol}Content-Length: {echoMessage.Length}{eoh}{echoMessage}";
        await WriteResponseAsync(stream, response);
        continue;
    }

    response = "HTTP/1.1 404 Not Found\r\n\r\n";
    await WriteResponseAsync(stream, response);
}

static async Task WriteResponseAsync(NetworkStream stream, string response)
{
    var responseBytes = Encoding.UTF8.GetBytes(response);
    Console.WriteLine($"Sending response: {response}");
    await stream.WriteAsync(responseBytes);
}