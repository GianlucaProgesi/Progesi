#nullable enable
using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  public sealed class AxisVariableGetComponent : GH_Component
  {
    public AxisVariableGetComponent()
      : base("AxisVariable.Get", "AxGet",
        "Load a persisted axis from the Rhino document by Id.",
        "Progesi", "AxisVar")
    { }

    public override Guid ComponentGuid => new Guid("f7c3a9e2-4b81-4d6a-9f3e-2c8b5d1e0a47");
    protected override System.Drawing.Bitmap Icon => ProgesiIcons.DataEx;

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddBooleanParameter("Run", "Run", "Execute", GH_ParamAccess.item, false);
      p.AddIntegerParameter("Id", "Id", "Persisted axis Id.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddGenericParameter("Axis", "Ax", "Loaded axis handle.", GH_ParamAccess.item);
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

      if (!AxisVarGhSupport.TryLoadAxisById(da, 1, this, repo, out var axis))
        return;

      try
      {
        var handle = new AxisVarHandle(axis.Id, axis);
        da.SetData(0, new Grasshopper.Kernel.Types.GH_ObjectWrapper(handle));
        da.SetData(1, axis.ContentHash);
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
      }
    }
  }
}
