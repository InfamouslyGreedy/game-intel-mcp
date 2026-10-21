using System.Net;

namespace GameIntel.Tests.TestDoubles;

/// <summary>Replays a queue of canned responses and records every request it sees.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public StubHttpHandler Respond(HttpStatusCode status, string body = "", Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body) };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public StubHttpHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"No canned response left for {request.RequestUri}.");
        }

        return Task.FromResult(_responses.Dequeue()(request));
    }
}

/// <summary>A clock tests can move forward.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
