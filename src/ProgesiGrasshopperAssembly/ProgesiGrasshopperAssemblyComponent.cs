using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using System;

namespace ProgesiGrasshopperAssembly
{
    public class ProgesiGrasshopperAssemblyComponent : GH_Component
    {
        public ProgesiGrasshopperAssemblyComponent()
          : base("Progesi Toolkit", "Progesi",
                 "Utilities and debug helpers for Progesi repositories",
                 "Progesi", "Debug")
        { }

        public override Guid ComponentGuid => new Guid("279341c6-27e7-4f34-a079-f8101ef0cc8f");

        protected override System.Drawing.Bitmap Icon => new System.Drawing.Bitmap(24, 24);

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            // No inputs: this is a placeholder component
        }

        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddTextParameter("Info", "I", "Component loaded", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            da.SetData(0, "Progesi Grasshopper assembly is loaded.");
        }
    }
}
