#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Grasshopper.Kernel;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using ProgesiRepositories.Rhino;
using Rhino.Geometry;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableOutComponent : GH_Component
  {
    public AxisVariableOutComponent()
      : base("AxisVariable.Out", "AxOut",
        "Read stations, labels, curve, and metadata from a persisted axis.",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("38c5f1fa-1639-47f3-b909-735c7f1c2d00");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Axis handle (optional when Id is set).", GH_ParamAccess.item);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id (when Axis is unwired).", GH_ParamAccess.item);
      Params.Input[1].Optional = true;
      Params.Input[2].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddIntegerParameter("Id", "Id", "Axis id.", GH_ParamAccess.item);
      p.AddTextParameter("AxisName", "AxN", "Axis name.", GH_ParamAccess.item);
      p.AddTextParameter("Name", "Nm", "Series name.", GH_ParamAccess.item);
      p.AddIntegerParameter("Mode", "M", "AxisCurveMode.", GH_ParamAccess.item);
      p.AddNumberParameter("Stations", "S", "Normalized stations.", GH_ParamAccess.list);
      p.AddNumberParameter("RealStations", "Rs", "Real arc-length stations.", GH_ParamAccess.list);
      p.AddTextParameter("Labels", "Lb", "Labels (pos;label pairs).", GH_ParamAccess.item);
      p.AddCurveParameter("Curve", "C", "3D axis curve.", GH_ParamAccess.item);
      p.AddTextParameter("Hash", "H", "Content hash.", GH_ParamAccess.item);
      p.AddIntegerParameter("VariableIds", "Vid", "ProgesiVariable ids per station.", GH_ParamAccess.list);
      p.AddGenericParameter("Values", "V", "ProgesiVariable values per station.", GH_ParamAccess.list);
      // TODO(B3c): expose PlaneService UVW + true-vertical frames on Out
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;

      if (!AxisVarGhSupport.TryLoadAxis(da, 1, this, repo, out var axis, optionalIdInputIndex: 2))
        return;

      if (!AxisVarGhSupport.TryDecodeCurve(axis, this, out var curve) || curve == null)
        return;

      try
      {
        var mapper = AxisVarGhSupport.CreateMapper(curve, axis.Mode);
        var normalized = axis.KeyPoints.ToList();
        if (normalized.Count == 0)
          normalized = axis.EnumerateAll().Select(e => e.positionNormalized).Distinct().OrderBy(x => x).ToList();

        var real = normalized.Select(n => mapper.NormalizedToReal(n)).ToList();
        var labelPairs = axis.GetLabels()
          .OrderBy(kv => kv.Key)
          .Select(kv => kv.Key.ToString("R", CultureInfo.InvariantCulture) + ";" + kv.Value);

        var entries = axis.EnumerateAll()
          .OrderBy(e => e.positionNormalized)
          .ThenBy(e => e.side)
          .ThenBy(e => e.variableId)
          .ToList();

        var varRepo = new RhinoVariableRepository(doc);
        var variableIds = new List<int>();
        var values = new List<object?>();
        foreach (var entry in entries)
        {
          variableIds.Add(entry.variableId);
          var variable = varRepo.GetByIdAsync(entry.variableId).GetAwaiter().GetResult();
          values.Add(variable?.Value);
        }

        da.SetData(0, axis.Id);
        da.SetData(1, axis.AxisName);
        da.SetData(2, axis.Name);
        da.SetData(3, (int)axis.Mode);
        da.SetDataList(4, normalized);
        da.SetDataList(5, real);
        da.SetData(6, string.Join("|", labelPairs));
        da.SetData(7, curve);
        da.SetData(8, axis.ContentHash);
        da.SetDataList(9, variableIds);
        da.SetDataList(10, values);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
