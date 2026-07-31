using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Progesi.LiveDataExchange.Cloud
{
  public sealed class ProgesiCloudClientOptions
  {
    public string BaseUrl { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public string TestRoles { get; set; } = string.Empty;
  }

  public sealed class HttpProgesiCloudClient : IProgesiCloudClient, IDisposable
  {
    private readonly HttpClient _http;
    private readonly ProgesiCloudClientOptions _options;
    private readonly bool _ownsHttpClient;

    public HttpProgesiCloudClient(ProgesiCloudClientOptions options)
        : this(options, new HttpClient(), ownsHttpClient: true)
    {
    }

    public HttpProgesiCloudClient(ProgesiCloudClientOptions options, HttpClient httpClient, bool ownsHttpClient = false)
    {
      _options = options ?? throw new ArgumentNullException(nameof(options));
      _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
      _ownsHttpClient = ownsHttpClient;
    }

    public async Task<CloudSnapshot> GetCloudSnapshotAsync(CancellationToken ct = default)
    {
      var variables = await GetAsync<List<WireVariableDto>>("/api/variables", ct).ConfigureAwait(false)
          ?? new List<WireVariableDto>();
      var metadata = await GetAsync<List<WireMetadataDto>>("/api/metadata", ct).ConfigureAwait(false)
          ?? new List<WireMetadataDto>();
      var clusters = await GetAsync<List<WireClusterDto>>("/api/clusters", ct).ConfigureAwait(false)
          ?? new List<WireClusterDto>();

      return new CloudSnapshot
      {
        Variables = variables.ConvertAll(MapVariable),
        Metadata = metadata.ConvertAll(MapMetadata),
        Clusters = clusters.ConvertAll(MapCluster)
      };
    }

    public Task UpsertVariableAsync(CloudVariableRecord record, CancellationToken ct = default)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      var payload = new WireVariableUpsertDto
      {
        Id = record.Id,
        Name = record.Name,
        Value = JsonConvert.DeserializeObject(record.ValueJson),
        DependsFrom = record.DependsFrom ?? Array.Empty<int>(),
        MetadataIds = record.MetadataIds ?? Array.Empty<int>(),
        IsAssumption = record.IsAssumption
      };

      return UpsertAsync("/api/variables", record.Id, payload, ct);
    }

    public Task UpsertMetadataAsync(CloudMetadataRecord record, CancellationToken ct = default)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      var payload = new WireMetadataUpsertDto
      {
        Id = record.Id,
        CreatedBy = record.CreatedBy,
        AdditionalInfo = record.AdditionalInfo,
        References = record.References ?? Array.Empty<string>(),
        Snips = MapSnips(record.Snips)
      };

      return UpsertAsync("/api/metadata", record.Id, payload, ct);
    }

    public Task UpsertClusterAsync(CloudClusterRecord record, CancellationToken ct = default)
    {
      if (record == null) throw new ArgumentNullException(nameof(record));

      var payload = new WireClusterUpsertDto
      {
        Id = record.Id,
        Name = record.Name,
        Description = record.Description,
        VariableIds = record.VariableIds ?? Array.Empty<int>()
      };

      return UpsertAsync("/api/clusters", record.Id, payload, ct);
    }

    public void Dispose()
    {
      if (_ownsHttpClient)
        _http.Dispose();
    }

    private async Task UpsertAsync(string collectionPath, int id, object payload, CancellationToken ct)
    {
      var put = await SendAsync(HttpMethod.Put, collectionPath + "/" + id, payload, ct).ConfigureAwait(false);
      if (put.StatusCode == HttpStatusCode.NotFound)
      {
        var post = await SendAsync(HttpMethod.Post, collectionPath, payload, ct).ConfigureAwait(false);
        EnsureSuccess(post);
        return;
      }

      EnsureSuccess(put);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
      using (var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false))
      {
        EnsureSuccess(response);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(json);
      }
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, object payload, CancellationToken ct)
    {
      var request = new HttpRequestMessage(method, CombineUrl(_options.BaseUrl, relativePath));
      ApplyHeaders(request);

      if (payload != null)
      {
        var json = JsonConvert.SerializeObject(payload);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
      }

      return _http.SendAsync(request, ct);
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
      if (!string.IsNullOrWhiteSpace(_options.ProjectId))
        request.Headers.TryAddWithoutValidation("X-Project-Id", _options.ProjectId.Trim());

      if (!string.IsNullOrWhiteSpace(_options.TestRoles))
      {
        request.Headers.TryAddWithoutValidation("X-Test-Roles", _options.TestRoles.Trim());
        return;
      }

      if (!string.IsNullOrWhiteSpace(_options.BearerToken))
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken.Trim());
    }

    private static string CombineUrl(string baseUrl, string relativePath)
    {
      if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Api base URL is required.");

      return baseUrl.TrimEnd('/') + relativePath;
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
      if (response.IsSuccessStatusCode)
        return;

      throw new HttpRequestException(
          "Cloud API request failed: " + (int)response.StatusCode + " " + response.ReasonPhrase);
    }

    private static CloudVariableRecord MapVariable(WireVariableDto dto)
    {
      return new CloudVariableRecord
      {
        Id = dto.Id,
        ContentHash = dto.Hashtag ?? string.Empty,
        Name = dto.Name ?? string.Empty,
        ValueJson = JsonConvert.SerializeObject(dto.Value),
        DependsFrom = dto.DependsFrom ?? Array.Empty<int>(),
        MetadataIds = dto.MetadataIds ?? Array.Empty<int>(),
        IsAssumption = dto.IsAssumption
      };
    }

    private static CloudMetadataRecord MapMetadata(WireMetadataDto dto)
    {
      return new CloudMetadataRecord
      {
        Id = dto.Id,
        ContentHash = dto.Hashtag ?? string.Empty,
        CreatedBy = dto.CreatedBy ?? string.Empty,
        AdditionalInfo = dto.AdditionalInfo ?? string.Empty,
        References = dto.References ?? Array.Empty<string>(),
        Snips = MapSnipsBack(dto.Snips)
      };
    }

    private static CloudClusterRecord MapCluster(WireClusterDto dto)
    {
      return new CloudClusterRecord
      {
        Id = dto.Id,
        ContentHash = dto.Hashtag ?? string.Empty,
        Name = dto.Name ?? string.Empty,
        Description = dto.Description ?? string.Empty,
        VariableIds = dto.VariableIds ?? Array.Empty<int>()
      };
    }

    private static WireMetadataSnipDto[] MapSnips(CloudMetadataSnipRecord[] snips)
    {
      if (snips == null || snips.Length == 0)
        return Array.Empty<WireMetadataSnipDto>();

      var mapped = new WireMetadataSnipDto[snips.Length];
      for (var i = 0; i < snips.Length; i++)
      {
        mapped[i] = new WireMetadataSnipDto
        {
          Id = snips[i].Id,
          MimeType = snips[i].MimeType,
          Caption = snips[i].Caption,
          Source = snips[i].Source,
          ContentBase64 = snips[i].ContentBase64
        };
      }

      return mapped;
    }

    private static CloudMetadataSnipRecord[] MapSnipsBack(WireMetadataSnipDto[] snips)
    {
      if (snips == null || snips.Length == 0)
        return Array.Empty<CloudMetadataSnipRecord>();

      var mapped = new CloudMetadataSnipRecord[snips.Length];
      for (var i = 0; i < snips.Length; i++)
      {
        mapped[i] = new CloudMetadataSnipRecord
        {
          Id = snips[i].Id,
          MimeType = snips[i].MimeType,
          Caption = snips[i].Caption,
          Source = snips[i].Source,
          ContentBase64 = snips[i].ContentBase64
        };
      }

      return mapped;
    }

    private sealed class WireVariableDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public object Value { get; set; }
      public int[] DependsFrom { get; set; }
      public int[] MetadataIds { get; set; }
      public bool IsAssumption { get; set; }
      public string Hashtag { get; set; }
    }

    private sealed class WireVariableUpsertDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public object Value { get; set; }
      public int[] DependsFrom { get; set; }
      public int[] MetadataIds { get; set; }
      public bool IsAssumption { get; set; }
    }

    private sealed class WireMetadataDto
    {
      public int Id { get; set; }
      public string CreatedBy { get; set; }
      public string AdditionalInfo { get; set; }
      public string[] References { get; set; }
      public WireMetadataSnipDto[] Snips { get; set; }
      public string Hashtag { get; set; }
    }

    private sealed class WireMetadataUpsertDto
    {
      public int Id { get; set; }
      public string CreatedBy { get; set; }
      public string AdditionalInfo { get; set; }
      public string[] References { get; set; }
      public WireMetadataSnipDto[] Snips { get; set; }
    }

    private sealed class WireMetadataSnipDto
    {
      public Guid Id { get; set; }
      public string MimeType { get; set; }
      public string Caption { get; set; }
      public string Source { get; set; }
      public string ContentBase64 { get; set; }
    }

    private sealed class WireClusterDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public string Description { get; set; }
      public int[] VariableIds { get; set; }
      public string Hashtag { get; set; }
    }

    private sealed class WireClusterUpsertDto
    {
      public int Id { get; set; }
      public string Name { get; set; }
      public string Description { get; set; }
      public int[] VariableIds { get; set; }
    }
  }
}
