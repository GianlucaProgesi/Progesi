using System;
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public class ProgesiVariableOutComponent : GH_Component
  {
    public ProgesiVariableOutComponent()
        : base("ProgesiVariableOut", "VarOut", "Get ProgesiVariable by Id (in-memory)",
               "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("B5C92E1C-5D2D-4D5D-A7A4-1E6F8B9A3E77");

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      p.AddTextParameter("Id", "Id", "Variable Id (Guid)", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      p.AddTextParameter("Id", "Id", "Id", GH_ParamAccess.item);
      p.AddTextParameter("Name", "N", "Name", GH_ParamAccess.item);
      p.AddTextParameter("Value", "V", "Value", GH_ParamAccess.item);
      p.AddTextParameter("Unit", "U", "Unit", GH_ParamAccess.item);
      p.AddTextParameter("Type", "T", "Type", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Info", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      string idText = "";
      DA.GetData(0, ref idText);

      if (!Guid.TryParse(idText, out var id))
      {
        DA.SetData(5, "Invalid Id");
        return;
      }

      try
      {
        var v = ServiceHub.GetVariable(id);
        if (v != null)
        {
          DA.SetData(0, v.Id.ToString());
          DA.SetData(1, v.Name ?? "");
          DA.SetData(2, v.Value.ToString());
          DA.SetData(3, v.Unit ?? "");
          DA.SetData(4, v.Type ?? "");
          DA.SetData(5, "OK");
        }
        else
        {
          DA.SetData(5, "Not found");
        }
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        DA.SetData(5, "Error: " + ex.Message);
      }
    }
  }
}
