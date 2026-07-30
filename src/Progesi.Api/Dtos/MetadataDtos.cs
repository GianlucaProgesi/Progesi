namespace Progesi.Api.Dtos;

public sealed class MetadataDto
{
  public int Id { get; set; }
  public DateTime LastModifiedUtc { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
  public string AdditionalInfo { get; set; } = string.Empty;
  public string[] References { get; set; } = Array.Empty<string>();
  public MetadataSnipDto[] Snips { get; set; } = Array.Empty<MetadataSnipDto>();
  public string Hashtag { get; set; } = string.Empty;
}

public sealed class MetadataUpsertDto
{
  public int Id { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
  public string AdditionalInfo { get; set; } = string.Empty;
  public string[] References { get; set; } = Array.Empty<string>();
  public MetadataSnipDto[] Snips { get; set; } = Array.Empty<MetadataSnipDto>();
}

public sealed class MetadataSnipDto
{
  public Guid Id { get; set; }
  public string MimeType { get; set; } = "image/png";
  public string Caption { get; set; } = string.Empty;
  public string? Source { get; set; }
  public string ContentBase64 { get; set; } = string.Empty;
}
