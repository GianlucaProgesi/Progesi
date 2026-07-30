namespace Progesi.Infrastructure.EF.Entities;

public sealed class AxisEntity
{
  public int Id { get; set; }
  public string AxisName { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string ValueTypeKey { get; set; } = string.Empty;
  public double? AxisLength { get; set; }
  public string CurvePayload { get; set; } = string.Empty;
  public int Mode { get; set; }
  public string KeyPointsJson { get; set; } = "[]";
  public int? RuleId { get; set; }
  public int? FunctionId { get; set; }
  public string? FunctionHashtag { get; set; }
  public string FunctionPayload { get; set; } = string.Empty;
  public string StationsJson { get; set; } = "[]";
  public string ContentHash { get; set; } = string.Empty;
  public string Hashtag { get; set; } = string.Empty;
}
