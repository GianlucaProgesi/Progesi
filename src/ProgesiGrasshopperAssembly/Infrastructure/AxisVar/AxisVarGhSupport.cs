#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Progesi.GrasshopperAssembly.Components;
using ProgesiCore;
using ProgesiRepositories.Rhino;
using Rhino;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar
{
  internal static class AxisVarGhSupport
  {
    internal static bool TryGetRun(IGH_DataAccess da, GH_Component owner, out bool run)
    {
      run = false;
      if (!da.GetData(0, ref run) || !run)
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run=True to execute.");
        return false;
      }
      return true;
    }

    internal static RhinoDoc? TryGetActiveDoc(GH_Component owner)
    {
      var doc = RhinoDoc.ActiveDoc;
      if (doc == null)
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "RhinoDoc.ActiveDoc is null.");
      return doc;
    }

    internal static RhinoAxisVariableRepository? TryGetAxisRepo(GH_Component owner, RhinoDoc doc)
    {
      RhinoBridgeBootstrap.EnsureConfigured();
      return new RhinoAxisVariableRepository(doc);
    }

    internal static bool TryUnwrapHandle(object? input, GH_Component owner, out AxisVarHandle handle)
    {
      handle = null!;
      if (input == null)
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axis input is required.");
        return false;
      }

      if (input is GH_ObjectWrapper ow && ow.Value != null)
        input = ow.Value;
      else if (input is IGH_Goo goo)
        input = goo.ScriptVariable();

      if (input is AxisVarHandle h)
      {
        handle = h;
        return true;
      }

      owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axis input is not an AxisVarHandle.");
      return false;
    }

    internal static bool TryLoadAxis(
      IGH_DataAccess da,
      int inputIndex,
      GH_Component owner,
      RhinoAxisVariableRepository repo,
      out ProgesiAxisVariable axis)
    {
      axis = null!;
      object? input = null;
      if (!da.GetData(inputIndex, ref input))
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axis input is required.");
        return false;
      }

      if (TryUnwrapHandle(input, owner, out var handle))
      {
        axis = handle.Axis;
        return true;
      }

      if (input is GH_Integer ghInt)
        input = ghInt.Value;
      else if (input is IGH_Goo gooInt && gooInt.CastTo(out int id))
        input = id;

      if (input is int axisId && axisId > 0)
      {
        var loaded = repo.GetByIdAsync(axisId).GetAwaiter().GetResult();
        if (loaded == null)
        {
          owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Axis id {axisId} not found.");
          return false;
        }
        axis = loaded;
        return true;
      }

      owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axis input must be an AxisVarHandle or positive Id.");
      return false;
    }

    internal static ProgesiCore.AxisCurveMode ParseMode(int modeInt)
      => (ProgesiCore.AxisCurveMode)Math.Max(0, Math.Min(2, modeInt));

    internal static bool TryDecodeCurve(ProgesiAxisVariable axis, GH_Component owner, out Curve? curve)
    {
      curve = null;
      if (string.IsNullOrWhiteSpace(axis.CurvePayload))
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Axis has no CurvePayload.");
        return false;
      }

      if (!ProgesiGeometryValueCodec.TryDecode(axis.CurvePayload, out var geom))
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "CurvePayload could not be decoded to a Rhino curve.");
        return false;
      }

      if (!(geom is Curve c))
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "CurvePayload could not be decoded to a Rhino curve.");
        return false;
      }

      curve = c;
      return true;
    }

    internal static CurveParameterMapper CreateMapper(Curve curve3d, ProgesiCore.AxisCurveMode mode)
      => new CurveParameterMapper(curve3d, mode);

    internal static int NextAxisId(RhinoAxisVariableRepository repo)
    {
      var all = repo.GetAllAsync().GetAwaiter().GetResult();
      return all.Count == 0 ? 1 : all.Max(a => a.Id) + 1;
    }

    internal static AxisVarHandle SaveAxis(RhinoAxisVariableRepository repo, ProgesiAxisVariable axis)
    {
      var saved = repo.SaveAsync(axis).GetAwaiter().GetResult();
      return new AxisVarHandle(saved.Id, saved);
    }

    internal static ProgesiAxisVariable CloneForEdit(ProgesiAxisVariable source)
    {
      var dto = ProgesiCore.Serialization.ProgesiAxisVariableDto.FromDomain(source);
      return ProgesiCore.Serialization.ProgesiAxisVariableDto.ToDomain(dto);
    }

    internal static void ApplyKeyPointsAndOptionalVariables(
      ProgesiAxisVariable axis,
      IReadOnlyList<double> normalizedStations,
      IReadOnlyList<int>? variableIds = null)
    {
      axis.SetKeyPoints(normalizedStations);
      if (variableIds == null || variableIds.Count == 0)
        return;

      if (variableIds.Count != normalizedStations.Count)
        throw new InvalidOperationException("VariableIds count must match station count.");

      var sig = new ProgesiAxisVariable.ProgesiVariableSignature(0, axis.Name, axis.ValueTypeKey);
      for (int i = 0; i < normalizedStations.Count; i++)
      {
        if (variableIds[i] <= 0) continue;
        sig = new ProgesiAxisVariable.ProgesiVariableSignature(variableIds[i], axis.Name, axis.ValueTypeKey);
        axis.Add(sig, normalizedStations[i]);
      }
    }

    internal static bool TryApplyVariation(
      IGH_DataAccess da,
      int axisInputIndex,
      GH_Component owner,
      RhinoAxisVariableRepository repo,
      IStationStrategy strategy,
      out AxisVarHandle? handle,
      out IReadOnlyList<double>? normalizedStations,
      out IReadOnlyList<double>? realStations,
      IReadOnlyList<double>? values = null,
      IReadOnlyList<int>? variableIds = null)
    {
      handle = null;
      normalizedStations = null;
      realStations = null;

      if (!TryLoadAxis(da, axisInputIndex, owner, repo, out var axis))
        return false;

      if (!TryDecodeCurve(axis, owner, out var curve) || curve == null)
        return false;

      try
      {
        var mapper = CreateMapper(curve, axis.Mode);
        var normalized = StationFactory.Create(strategy, mapper);
        var edited = CloneForEdit(axis);
        ApplyKeyPointsAndOptionalVariables(edited, normalized, variableIds);

        if (values != null && values.Count > 0)
        {
          if (values.Count != normalized.Count)
            throw new InvalidOperationException("Values count must match station count.");
          for (int i = 0; i < normalized.Count; i++)
            edited.SetLabel(normalized[i], values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        handle = SaveAxis(repo, edited);
        normalizedStations = normalized;
        realStations = normalized.Select(n => mapper.NormalizedToReal(n)).ToList();
        return true;
      }
      catch (Exception ex)
      {
        owner.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        return false;
      }
    }
  }
}
