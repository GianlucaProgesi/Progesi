using System.Globalization;
using Newtonsoft.Json;
using ProgesiCore;

namespace Progesi.Infrastructure.EF.Internal;

internal static class MetadataSerialization
{
  internal sealed class MetadataDto
  {
    public int Id { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? AdditionalInfo { get; set; }
    public List<string>? References { get; set; }
    public List<SnipDto>? Snips { get; set; }

    public static MetadataDto FromDomain(ProgesiMetadata m)
    {
      return new MetadataDto
      {
        Id = m.Id,
        LastModifiedUtc = m.LastModified.ToUniversalTime(),
        CreatedBy = m.CreatedBy,
        AdditionalInfo = m.AdditionalInfo,
        References = m.References?.Select(u => u.ToString()).ToList(),
        Snips = m.Snips?.Select(s => new SnipDto
        {
          Id = s.Id,
          MimeType = s.MimeType,
          Caption = s.Caption,
          Source = s.Source,
          ContentBase64 = Convert.ToBase64String(s.Content ?? Array.Empty<byte>())
        }).ToList()
      };
    }
  }

  internal sealed class SnipDto
  {
    public Guid Id { get; set; }
    public string MimeType { get; set; } = "image/png";
    public string Caption { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string ContentBase64 { get; set; } = string.Empty;
  }

  public static string ToJson(ProgesiMetadata metadata)
  {
    var dto = MetadataDto.FromDomain(metadata);
    return JsonConvert.SerializeObject(dto);
  }

  public static ProgesiMetadata? FromStoredJson(string json, string lastModifiedStr, int id)
  {
    var dto = JsonConvert.DeserializeObject<MetadataDto>(json);
    if (dto is null) return null;

    var lastModified = DateTime.Parse(lastModifiedStr, null, DateTimeStyles.RoundtripKind);

    var refs = dto.References?.Select(s => new Uri(s, UriKind.RelativeOrAbsolute));
    var snips = dto.Snips?.Select(s => ProgesiSnip.Create(
      Convert.FromBase64String(s.ContentBase64 ?? string.Empty),
      s.MimeType ?? "application/octet-stream",
      s.Caption,
      string.IsNullOrWhiteSpace(s.Source) ? null : new Uri(s.Source, UriKind.RelativeOrAbsolute)));

    return ProgesiMetadata.Create(
      dto.CreatedBy ?? string.Empty,
      dto.AdditionalInfo,
      refs,
      snips,
      lastModified,
      id);
  }
}
