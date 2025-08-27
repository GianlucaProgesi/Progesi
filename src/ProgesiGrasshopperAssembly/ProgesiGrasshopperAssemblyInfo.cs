using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace ProgesiGrasshopperAssembly
{
    public class ProgesiGrasshopperAssemblyInfo : GH_AssemblyInfo
    {
        public override string Name => "ProgesiGrasshopperAssembly";

        // 24x24 non-null placeholder icon to satisfy NRT
        public override Bitmap Icon => new Bitmap(24, 24);

        public override string Description => "Progesi Grasshopper components";

        public override Guid Id => new Guid("e25c9ed1-efc6-4705-a08b-cc926e105e98");

        public override string AuthorName => "Progesi";

        public override string AuthorContact => "info@progesi.example";

        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
