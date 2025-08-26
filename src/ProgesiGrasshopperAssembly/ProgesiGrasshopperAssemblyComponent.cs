using Grasshopper;
using Grasshopper.Kernel;
using ProgesiCore;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace ProgesiGrasshopperAssembly
{
    public class ProgesiGrasshopperAssemblyComponent : GH_Component
    {
        private static string filePath = "C:\\Users\\gianl\\source\\repos\\ProgesiGrasshopperAssembly\\ProgesiRhinoFakeRepository\\RhinoFakeRepository.3dm";
        RhinoDoc RhinoFakeRepository;
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ProgesiGrasshopperAssemblyComponent()
          : base("ProgesiGrasshopperAssemblyComponent", "Nickname",
            "Description",
            "Category", "Subcategory")
        {
         
        RhinoFakeRepository = RhinoDoc.FromFilePath("C:\\Users\\gianl\\source\\repos\\ProgesiGrasshopperAssembly\\ProgesiRhinoFakeRepository\\RhinoFakeRepository.3dm");

            RhinoFakeRepository = RhinoDoc.FromFilePath("C:\\Users\\gianl\\source\\repos\\ProgesiGrasshopperAssembly\\ProgesiRhinoFakeRepository\\RhinoFakeRepository.3dm");
            RhinoDoc.Open(filePath, out var doc);
            RhinoApp.WriteLine("RhinoFakeRepository loaded successfully.");

        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => new Guid("279341c6-27e7-4f34-a079-f8101ef0cc8f");
    }
}