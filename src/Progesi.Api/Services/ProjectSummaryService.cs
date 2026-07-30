using Progesi.Api.Dtos;
using ProgesiCore;

namespace Progesi.Api.Services;

public interface IProjectSummaryService
{
  Task<SummaryDto> GetSummaryAsync(CancellationToken ct = default);
  Task<ValueTypeBreakdownDto> GetValueTypeBreakdownAsync(CancellationToken ct = default);
}

public sealed class ProjectSummaryService : IProjectSummaryService
{
  private readonly IVariableRepository _variables;
  private readonly IMetadataRepository _metadata;
  private readonly IProgesiVariableClusterRepository _clusters;

  public ProjectSummaryService(
      IVariableRepository variables,
      IMetadataRepository metadata,
      IProgesiVariableClusterRepository clusters)
  {
    _variables = variables;
    _metadata = metadata;
    _clusters = clusters;
  }

  public async Task<SummaryDto> GetSummaryAsync(CancellationToken ct = default)
  {
    var variables = await _variables.GetAllAsync(ct);
    var metadata = await ListAllMetadataAsync(ct);
    var clusters = await _clusters.GetAllAsync(ct);

    var variablesWithMetadata = variables.Count(v => v.MetadataIds.Length > 0);
    var variableCount = variables.Count;
    var clusterCount = clusters.Count;

    var distinctClusterVariables = clusters
        .SelectMany(c => c.ProgesiVariableIds)
        .Distinct()
        .Count();

    var averageVariablesPerCluster = clusterCount == 0
        ? 0d
        : clusters.Average(c => c.ProgesiVariableIds.Count);

    return new SummaryDto
    {
      VariableCount = variableCount,
      MetadataCount = metadata.Count,
      ClusterCount = clusterCount,
      VariablesWithMetadataCount = variablesWithMetadata,
      MetadataCoveragePercent = variableCount == 0
          ? 0d
          : Math.Round(100d * variablesWithMetadata / variableCount, 2),
      ClusterMembership = new ClusterMembershipSummaryDto
      {
        DistinctVariablesReferenced = distinctClusterVariables,
        AverageVariablesPerCluster = Math.Round(averageVariablesPerCluster, 2)
      },
      ValueTypeBreakdown = BuildValueTypeBreakdown(variables)
    };
  }

  public async Task<ValueTypeBreakdownDto> GetValueTypeBreakdownAsync(CancellationToken ct = default)
  {
    var variables = await _variables.GetAllAsync(ct);
    return BuildValueTypeBreakdown(variables);
  }

  private async Task<List<ProgesiMetadata>> ListAllMetadataAsync(CancellationToken ct)
  {
    const int pageSize = 500;
    var all = new List<ProgesiMetadata>();
    var skip = 0;

    while (true)
    {
      var page = await _metadata.ListAsync(skip, pageSize, ct);
      if (page.Count == 0)
        break;

      all.AddRange(page);
      if (page.Count < pageSize)
        break;

      skip += pageSize;
    }

    return all;
  }

  internal static ValueTypeBreakdownDto BuildValueTypeBreakdown(IReadOnlyList<ProgesiVariable> variables)
  {
    var breakdown = new ValueTypeBreakdownDto();

    foreach (var variable in variables)
    {
      switch (ClassifyValueType(variable.Value))
      {
        case "string":
          breakdown.String++;
          break;
        case "int":
          breakdown.Int++;
          break;
        case "double":
          breakdown.Double++;
          break;
        case "bool":
          breakdown.Bool++;
          break;
        default:
          breakdown.Object++;
          break;
      }
    }

    return breakdown;
  }

  internal static string ClassifyValueType(object? value)
  {
    return value switch
    {
      null => "object",
      string => "string",
      bool => "bool",
      int or long or short or byte or sbyte or ushort or uint or ulong => "int",
      float or double or decimal => "double",
      _ => "object"
    };
  }
}
