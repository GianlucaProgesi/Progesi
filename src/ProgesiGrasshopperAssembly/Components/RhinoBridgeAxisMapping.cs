#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProgesiCore;
using ProgesiRepositories.Rhino;
using ExchangeAxisDto = Progesi.DataExchange.ProgesiAxisVariableDto;
using CoreAxisDto = ProgesiCore.Serialization.ProgesiAxisVariableDto;
using Rhino;

namespace Progesi.GrasshopperAssembly.Components
{
  internal static class RhinoBridgeAxisMapping
  {
    internal static ExchangeAxisDto ToExchangeDto(
      ProgesiAxisVariable axis,
      RhinoVariableRepository variableRepo)
    {
      var stations = new StringBuilder();
      var hashes = new StringBuilder();
      bool first = true;
      foreach (var entry in axis.EnumerateAll().OrderBy(e => e.positionNormalized).ThenBy(e => e.side).ThenBy(e => e.variableId))
      {
        if (!first)
        {
          stations.Append(';');
          hashes.Append(';');
        }
        first = false;
        stations.Append(entry.positionNormalized.ToString("R", CultureInfo.InvariantCulture));
        var variable = variableRepo.GetByIdAsync(entry.variableId).GetAwaiter().GetResult();
        hashes.Append(variable?.Hashtag ?? entry.variableId.ToString(CultureInfo.InvariantCulture));
      }

      return new ExchangeAxisDto
      {
        Id = axis.Id.ToString(CultureInfo.InvariantCulture),
        Hash = ProgesiHash.Compute(axis),
        Name = axis.Name ?? string.Empty,
        ValueTypeKey = axis.ValueTypeKey ?? string.Empty,
        Unit = string.Empty,
        AxisRef = axis.AxisName ?? string.Empty,
        Stations = stations.ToString(),
        VariableHashes = hashes.ToString(),
        By = string.Empty,
        Ref = string.Empty,
        LastModifiedUtc = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture) + "Z"
      };
    }

    internal static ProgesiAxisVariable ToDomain(
      ExchangeAxisDto dto,
      int axisId,
      RhinoVariableRepository variableRepo,
      ProgesiAxisVariable? current)
    {
      if (!ExchangeAxisDto.TryParseSeries(dto.Stations ?? string.Empty, out var stations))
        throw new InvalidOperationException("Invalid Stations series.");
      if (!ExchangeAxisDto.TryParseTokens(dto.VariableHashes ?? string.Empty, out var hashTokens))
        throw new InvalidOperationException("Invalid VariableHashes series.");
      if (stations.Count != hashTokens.Count)
        throw new InvalidOperationException("Stations and VariableHashes count mismatch.");

      var coreDto = new CoreAxisDto
      {
        AxisId = axisId,
        AxisName = string.IsNullOrWhiteSpace(dto.AxisRef) ? dto.Name ?? string.Empty : dto.AxisRef,
        Name = dto.Name ?? string.Empty,
        ValueTypeKey = dto.ValueTypeKey ?? "System.Double",
        Mode = current?.Mode ?? AxisCurveMode.Curve3d,
        CurvePayload = current?.CurvePayload ?? string.Empty,
        KeyPoints = current?.KeyPoints.ToList() ?? new List<double>(),
        RuleId = current?.RuleId,
        FunctionId = current?.FunctionRef.FunctionId,
        FunctionHashtag = current?.FunctionRef.FunctionHashtag,
        FunctionPayload = current?.FunctionRef.Embedded?.ToJson(),
        ContentHash = dto.Hash ?? string.Empty,
        Entries = new List<CoreAxisDto.Entry>(),
        Labels = current != null
          ? current.GetLabels().Select(kv => new CoreAxisDto.LabelEntry
          {
            Position = kv.Key,
            Label = kv.Value
          }).ToList()
          : new List<CoreAxisDto.LabelEntry>()
      };

      for (int i = 0; i < stations.Count; i++)
      {
        var variable = variableRepo.GetByHashtagAsync(hashTokens[i]).GetAwaiter().GetResult();
        if (variable == null)
          continue;
        coreDto.Entries.Add(new CoreAxisDto.Entry
        {
          Position = stations[i],
          VariableId = variable.Id,
          Side = ProgesiAxisStationSide.None
        });
      }

      return CoreAxisDto.ToDomain(coreDto);
    }
  }
}
