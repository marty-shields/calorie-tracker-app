using System.Net;
using System.Text;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>Plays queued responses in order, the last one repeating, and records each request.</summary>
internal sealed class FakeSearchHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> queue = new();
    private Func<HttpResponseMessage>? last;

    public List<(Uri Uri, string UserAgent)> Requests { get; } = [];

    /// <summary>The decoded <c>q</c> of each request.</summary>
    public IEnumerable<string> Queries => Requests.Select(r => Uri.UnescapeDataString(r.Uri.Query.TrimStart('?').Split('&').First(p => p.StartsWith("q=", StringComparison.Ordinal))[2..]));

    public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        queue.Enqueue(() => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    public void Throw(Exception exception) => queue.Enqueue(() => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.RequestUri!, request.Headers.UserAgent.ToString()));
        if (queue.Count > 0)
        {
            last = queue.Dequeue();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(last!());
    }
}
