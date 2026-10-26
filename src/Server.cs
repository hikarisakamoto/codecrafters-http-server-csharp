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
    Console.WriteLine("Client connected, reading request...");
    // Read the request before replying; closing with unread data would reset the connection.
    var buffer = new byte[1_024];
    var bytesRead = await stream.ReadAsync(buffer);
    Console.WriteLine($"Request read ({bytesRead} bytes), sending response...");
    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    var requestLines = request.Split("\r\n");
    var requestLine = requestLines[0];
    var requestParts = requestLine.Split(' ');

    var target = requestParts[1];
    var response = string.Empty;
    Console.WriteLine($"Processing request for target: {target}");
    // Actually, reading dotnet core source code to learn this and how pro do it
    switch (target)
    {
        case "/":
            response = $"{httpOkResponse}{eoh}"; 
            break;
        default:
            response = "HTTP/1.1 404 Not Found\r\n\r\n";
            break;
    }
    
    Console.WriteLine($"Responding with: {response}");
    await stream.WriteAsync(Encoding.UTF8.GetBytes(response));
}