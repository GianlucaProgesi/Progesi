#nullable enable
using System;
using System.Linq;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableInheritFromComponent : GH_Component
  {
    public AxisVariableInheritFromComponent()
      : base("AxisVariable.InheritFrom", "AxInh",
        "Reuse normalized stations from another axis.",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("b4c5d6e7-8596-abcd-ef01-234567890123");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddGenericParameter("Axis", "Ax", "Target axis handle or Id.", GH_ParamAccess.item);
      p.AddGenericParameter("SourceAxis", "Src", "Source axis handle or Id.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Axis", "Ax", "Updated axis handle.", GH_ParamAccess.item);
      p.AddNumberParameter("Stations", "S", "Normalized stations.", GH_ParamAccess.list);
      p.AddNumberParameter("RealStations", "Rs", "Real stations on target axis.", GH_ParamAccess.list);
      p.AddTextParameter("Hash", "H", "Content hash.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess da)
    {
      if (!AxisVarGhSupport.TryGetRun(da, this, out _))
        return;

      var doc = AxisVarGhSupport.TryGetActiveDoc(this);
      if (doc == null) return;
      var repo = AxisVarGhSupport.TryGetAxisRepo(this, doc);
      if (repo == null) return;

      if (!AxisVarGhSupport.TryLoadAxis(da, 2, this, repo, out var sourceAxis))
        return;

      var inherited = sourceAxis.KeyPoints.ToList();
      if (inherited.Count == 0)
        inherited = sourceAxis.EnumerateAll().Select(e => e.positionNormalized).Distinct().OrderBy(x => x).ToList();

      if (inherited.Count == 0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Source axis has no stations to inherit.");
        return;
      }

      if (!AxisVarGhSupport.TryApplyVariation(
            da, 1, this, repo,
            new InheritFromStrategy(inherited),
            out var handle, out var normalized, out var real))
        return;

      da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
      da.SetDataList(1, normalized);
      da.SetDataList(2, real);
      da.SetData(3, handle!.Axis.ContentHash);
    }
  }
}
