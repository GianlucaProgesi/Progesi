using Progesi.Api.Auth;

namespace Progesi.Api.Tests;

internal static class TestAuthClientExtensions
{
  public static HttpClient AsReader(this HttpClient client)
  {
    client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeaderName);
    client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, AuthRoles.Reader);
    return client;
  }

  public static HttpClient AsWriter(this HttpClient client)
  {
    client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeaderName);
    client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, AuthRoles.Writer);
    return client;
  }

  public static HttpClient AsUnauthenticated(this HttpClient client)
  {
    client.DefaultRequestHeaders.Remove(TestAuthHandler.RolesHeaderName);
    return client;
  }
}
