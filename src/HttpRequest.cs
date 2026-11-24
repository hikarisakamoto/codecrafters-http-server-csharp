using System.Net.Http.Headers;

/// <summary>A parsed HTTP/1.1 request. <see cref="Method"/> and <see cref="Target"/> are empty when the request line is malformed.</summary>
internal sealed record HttpRequest(string Method, string Target, IReadOnlyDictionary<string, string> Headers, string Body)
{
    /// <summary>Reads the next request from the connection, or returns null once the client disconnects.</summary>
    public static async Task<HttpRequest?> ReadAsync(StreamReader reader)
    {
        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) is { Length: > 0 })
        {
            lines.Add(line);
        }

        if (line is null)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in lines.Skip(1))
        {
            var separator = header.IndexOf(':');
            if (separator > 0)
            {
                headers.TryAdd(header[..separator], header[(separator + 1)..].Trim());
            }
        }

        var body = "";
        if (int.TryParse(headers.GetValueOrDefault("Content-Length"), out var length) && length > 0)
        {
            var buffer = new char[length];
            var read = await reader.ReadBlockAsync(buffer, 0, length);
            if (read != length)
            {
                Console.Error.WriteLine($"Expected to read {length} bytes for request body, but only read {read} bytes.");
                return null;
            }

            body = new string(buffer);
        }

        return lines.FirstOrDefault()?.Split(' ') is [var method, var target, _]
            ? new HttpRequest(method.ToUpperInvariant(), target, headers, body)
            : new HttpRequest("", "", headers, body);
    }

    /// <summary>Returns the header value, or an empty string when absent.</summary>
    public string Header(string name) => Headers.GetValueOrDefault(name, "");

    /// <summary>Whether Accept-Encoding allows a gzip response (explicitly or via <c>*</c>, with q &gt; 0).</summary>
    public bool AcceptsGzip()
    {
        var encodings = Header("Accept-Encoding")
            .Split(',')
            .Select(value => StringWithQualityHeaderValue.TryParse(value.Trim(), out var encoding) ? encoding : null)
            .OfType<StringWithQualityHeaderValue>()
            .ToArray();

        var gzip = encodings.FirstOrDefault(encoding => encoding.Value.Equals("gzip", StringComparison.OrdinalIgnoreCase))
            ?? encodings.FirstOrDefault(encoding => encoding.Value == "*");

        return gzip is not null && gzip.Quality.GetValueOrDefault(1) > 0;
    }
}
