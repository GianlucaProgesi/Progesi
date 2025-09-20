using System;
using Grasshopper.Kernel;
using Progesi.DomainServices.Models;
using ProgesiGrasshopperAssembly.Infrastructure;

namespace ProgesiGrasshopperAssembly.Components
{
  public class ProgesiVariableInComponent : GH_Component
  {
    public ProgesiVariableInComponent()
        : base("ProgesiVariableIn", "VarIn", "Create/Update/Delete ProgesiVariable (in-memory)",
               "Progesi", "Variables")
    { }

    public override Guid ComponentGuid => new Guid("E7F6D4B1-0F7E-4A7B-9F6D-5C0C5D8C1A10");

    protected override void RegisterInputParams(GH_InputParamManager p)
    {
      // A: Activate, Id: Guid (optional), N: Name (required), V: Value, U: Unit, T: Type
      p.AddBooleanParameter("Activate", "A", "Execute Create/Update when true.", GH_ParamAccess.item, true);
      p.AddTextParameter("Id", "Id", "Existing variable Id (Guid). Leave empty for create.", GH_ParamAccess.item);
      p.AddTextParameter("Name", "N", "Variable name (required).", GH_ParamAccess.item);
      p.AddNumberParameter("Value", "V", "Numeric value.", GH_ParamAccess.item, 0.0);
      p.AddTextParameter("Unit", "U", "Unit.", GH_ParamAccess.item, "");
      p.AddTextParameter("Type", "T", "Type (e.g., double).", GH_ParamAccess.item, "");
    }

    protected override void RegisterOutputParams(GH_OutputParamManager p)
    {
      // Id, N, V, U, T, Info
      p.AddTextParameter("Id", "Id", "Variable id (Guid).", GH_ParamAccess.item);
      p.AddTextParameter("Name", "N", "Name.", GH_ParamAccess.item);
      p.AddNumberParameter("Value", "V", "Value.", GH_ParamAccess.item);
      p.AddTextParameter("Unit", "U", "Unit.", GH_ParamAccess.item);
      p.AddTextParameter("Type", "T", "Type.", GH_ParamAccess.item);
      p.AddTextParameter("Info", "Info", "Operation result.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      bool activate = true;
      string idText = null, name = null, unit = "", type = "";
      double value = 0.0;

      DA.GetData(0, ref activate);
      DA.GetData(1, ref idText);
      DA.GetData(2, ref name);
      DA.GetData(3, ref value);
      DA.GetData(4, ref unit);
      DA.GetData(5, ref type);

      void Emit(string id, string n, double v, string u, string t, string info)
      {
        DA.SetData(0, id ?? "");
        DA.SetData(1, n ?? "");
        DA.SetData(2, v);
        DA.SetData(3, u ?? "");
        DA.SetData(4, t ?? "");
        DA.SetData(5, info ?? "");
      }

      if (!activate)
      {
        Emit("", "", 0.0, "", "", "Idle");
        return;
      }

      if (string.IsNullOrWhiteSpace(name))
      {
        const string msg = "Name is required.";
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
        Emit("", "", 0.0, "", "", msg);
        return;
      }

      try
      {
        Guid id = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(idText))
          Guid.TryParse(idText, out id);

        var input = new ProgesiVariable
        {
          Id = id,
          Name = name.Trim(),
          Unit = unit ?? "",
          Type = type ?? "",
          Value = value
        };

        var saved = ServiceHub.CreateOrUpdate(input);
        if (saved != null)
          Emit(saved.Id.ToString(), saved.Name, saved.Value, saved.Unit, saved.Type, "OK");
        else
          Emit("", "", 0.0, "", "", "Not created");
      }
      catch (Exception ex)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
        Emit("", "", 0.0, "", "", "Error: " + ex.Message);
      }
    }
  }
}
