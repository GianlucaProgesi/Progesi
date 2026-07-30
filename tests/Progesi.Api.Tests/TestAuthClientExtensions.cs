using Progesi.Api.Auth;
using Progesi.Api.Projects;

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

  public static HttpClient WithProject(this HttpClient client, string projectId)
  {
    client.DefaultRequestHeaders.Remove(ProjectHeaders.ProjectId);
    client.DefaultRequestHeaders.Add(ProjectHeaders.ProjectId, projectId);
    return client;
  }

  public static HttpClient AsWriterForProject(this HttpClient client, string projectId)
    => client.AsWriter().WithProject(projectId);

  public static HttpClient AsReaderForProject(this HttpClient client, string projectId)
    => client.AsReader().WithProject(projectId);
}
