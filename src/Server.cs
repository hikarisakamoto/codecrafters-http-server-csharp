using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
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

            var response = RouteRequest(requestLines, directory);
            await stream.WriteAsync(response);
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
    var contentLength = 0;

    while (true)
    {
        var line = await reader.ReadLineAsync();
        if (line is null)
        {
            return null;
        }

        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
        {
            var contentLengthValue = line["Content-Length:".Length..].Trim();
            if (int.TryParse(contentLengthValue, out var parsedLength))
            {
                contentLength = parsedLength;
            }
        }

        if (line.Length == 0)
        {
            break;
        }

        requestLines.Add(line);
    }

    if (contentLength > 0)
    {
        var bodyBuffer = new char[contentLength];
        var readLength = await reader.ReadBlockAsync(bodyBuffer, 0, contentLength);
        if (readLength != contentLength)
        {
            Console.Error.WriteLine($"Expected to read {contentLength} bytes for request body, but only read {readLength} bytes.");
            return null;
        }

        requestLines.Add(new string(bodyBuffer));
    }

    return requestLines;
}

static byte[] RouteRequest(List<string> requestLines, string directory)
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

    var method = requestLineParts[0];
    var target = requestLineParts[1];

    if (target == "/")
    {
        return CreateResponse("200 OK");
    }

    if (target.StartsWith("/files/", StringComparison.Ordinal))
    {
        var fileName = target["/files/".Length..];
        var path = Path.Combine(directory, fileName);

        Console.WriteLine($"File request: target='{target}', fileName='{fileName}', path='{path}'");
        return HandleFileRequest(method, path, requestLines[^1]);
    }

    if (target.StartsWith("/echo/", StringComparison.Ordinal))
    {
        return CreateResponse("200 OK", body: target["/echo/".Length..], useGzip: AcceptsGzip(requestLines));
    }

    if (target == "/user-agent")
    {
        return CreateResponse("200 OK", body: GetHeaderValue(requestLines, "User-Agent"));
    }

    return CreateResponse("404 Not Found");
}

static byte[] HandleFileRequest(string method, string path, string body)
{
    Console.WriteLine($"Reading file: '{path}'");
    if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: '{path}'. Returning 404 Not Found.");
            return CreateResponse("404 Not Found");
        }

        var fileContents = File.ReadAllText(path);
        Console.WriteLine($"File read: '{path}', {Encoding.UTF8.GetByteCount(fileContents)} response bytes. Returning 200 OK.");

        return CreateResponse("200 OK", "application/octet-stream", fileContents);
    }

    if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
    {
        File.WriteAllText(path, body);
        return CreateResponse("201 Created");
    }

    return CreateResponse("404 Not Found");
}

static string GetHeaderValue(List<string> requestLines, string headerName)
{
    var prefix = headerName + ":";
    var header = requestLines.Skip(1).FirstOrDefault(line =>
        line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    return header?[prefix.Length..].Trim() ?? string.Empty;
}

static bool AcceptsGzip(List<string> requestLines)
{
    var encodings = GetHeaderValue(requestLines, "Accept-Encoding")
        .Split(',')
        .Select(value => StringWithQualityHeaderValue.TryParse(value.Trim(), out var encoding) ? encoding : null)
        .OfType<StringWithQualityHeaderValue>()
        .ToArray();

    var gzip = encodings.FirstOrDefault(encoding => encoding.Value.Equals("gzip", StringComparison.OrdinalIgnoreCase))
        ?? encodings.FirstOrDefault(encoding => encoding.Value == "*");

    return gzip is not null && gzip.Quality.GetValueOrDefault(1) > 0;
}

static byte[] Compress(byte[] body)
{
    using var output = new MemoryStream();
    using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
    {
        gzip.Write(body);
    }

    return output.ToArray();
}

static byte[] CreateResponse(string status, string contentType = "text/plain", string body = "", bool useGzip = false)
{
    var bodyBytes = Encoding.UTF8.GetBytes(body);
    if (useGzip)
    {
        bodyBytes = Compress(bodyBytes);
    }

    var headers =
        $"HTTP/1.1 {status}\r\n" +
        $"Content-Type: {contentType}\r\n" +
        $"Content-Length: {bodyBytes.Length}\r\n" +
        (useGzip ? "Content-Encoding: gzip\r\n" : "");
        // +
        //"Connection: close\r\n\r\n";

    return [.. Encoding.ASCII.GetBytes(headers), .. bodyBytes];
}
