namespace Progesi.Api.Dtos;

public sealed class SummaryDto
{
  public int VariableCount { get; set; }
  public int MetadataCount { get; set; }
  public int ClusterCount { get; set; }
  public int VariablesWithMetadataCount { get; set; }
  public double MetadataCoveragePercent { get; set; }
  public ClusterMembershipSummaryDto ClusterMembership { get; set; } = new();
  public ValueTypeBreakdownDto ValueTypeBreakdown { get; set; } = new();
}

public sealed class ClusterMembershipSummaryDto
{
  public int DistinctVariablesReferenced { get; set; }
  public double AverageVariablesPerCluster { get; set; }
}

public sealed class ValueTypeBreakdownDto
{
  public int String { get; set; }
  public int Int { get; set; }
  public int Double { get; set; }
  public int Bool { get; set; }
  public int Object { get; set; }
}
