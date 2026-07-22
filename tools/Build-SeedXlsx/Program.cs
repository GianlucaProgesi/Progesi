using System;
using System.IO;
using ClosedXML.Excel;

class Program
{
    static void Main()
    {
        var repo = Directory.GetCurrentDirectory();
        var outDir = Path.GetFullPath(Path.Combine(repo, @"..\..\out"));
        Directory.CreateDirectory(outDir);
        var xlsx = Path.Combine(outDir, "progesi_seed.xlsx");
        var lm   = "2025-10-12T00:00:00";

        using (var wb = new XLWorkbook())
        {
            // ProgesiVariable
            var v = wb.AddWorksheet("ProgesiVariable");
            v.Cell(1,1).Value = "Id";   v.Cell(1,2).Value = "Hash";                v.Cell(1,3).Value = "Name";
            v.Cell(1,4).Value = "Value";v.Cell(1,5).Value = "Unit";                v.Cell(1,6).Value = "By";
            v.Cell(1,7).Value = "LM";   v.Cell(1,8).Value = "Info";

            v.Cell(2,1).Value = 1;  v.Cell(2,2).Value = "v_9a63c8f2a1e4a3d0"; v.Cell(2,3).Value = "Length";
            v.Cell(2,4).Value = "100.0"; v.Cell(2,5).Value = "mm"; v.Cell(2,6).Value = "beta-user"; v.Cell(2,7).Value = lm; v.Cell(2,8).Value = "seed var";

            v.Cell(3,1).Value = 2;  v.Cell(3,2).Value = "v_934d1af26b6bcb12"; v.Cell(3,3).Value = "Width";
            v.Cell(3,4).Value = "35.5";  v.Cell(3,5).Value = "mm"; v.Cell(3,6).Value = "beta-user"; v.Cell(3,7).Value = lm; v.Cell(3,8).Value = "seed var";

            // ProgesiMetadata
            var m = wb.AddWorksheet("ProgesiMetadata");
            m.Cell(1,1).Value = "Id"; m.Cell(1,2).Value = "Hash"; m.Cell(1,3).Value = "By";
            m.Cell(1,4).Value = "Refs"; m.Cell(1,5).Value = "Snips"; m.Cell(1,6).Value = "LM"; m.Cell(1,7).Value = "Info";

            m.Cell(2,1).Value = 1;  m.Cell(2,2).Value = "m_2b3d9f8a11a4c7e1"; m.Cell(2,3).Value = "beta-user";
            m.Cell(2,4).Value = "https://example.com/specs/part-01; data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHVgL1kK2A7gAAAABJRU5ErkJggg==";
            m.Cell(2,5).Value = "snip:1:image/png:caption=part 01"; m.Cell(2,6).Value = lm; m.Cell(2,7).Value = "seed metadata";

            m.Cell(3,1).Value = 2;  m.Cell(3,2).Value = "m_1c4f8a0b22d5e6f7"; m.Cell(3,3).Value = "beta-user";
            m.Cell(3,4).Value = "https://example.com/specs/part-02"; m.Cell(3,5).Value = ""; m.Cell(3,6).Value = lm; m.Cell(3,7).Value = "seed metadata";

            // opzionale: ProgesiAxisVariable (stesso layout di Variable)
            var a = wb.AddWorksheet("ProgesiAxisVariable");
            a.Cell(1,1).Value="Id"; a.Cell(1,2).Value="Hash"; a.Cell(1,3).Value="Name";
            a.Cell(1,4).Value="Value"; a.Cell(1,5).Value="Unit"; a.Cell(1,6).Value="By"; a.Cell(1,7).Value="LM"; a.Cell(1,8).Value="Info";
            a.Cell(2,1).Value=1; a.Cell(2,2).Value="ax_0"; a.Cell(2,3).Value="AxisX"; a.Cell(2,4).Value="0.0"; a.Cell(2,5).Value=""; a.Cell(2,6).Value="beta-user"; a.Cell(2,7).Value=lm; a.Cell(2,8).Value="demo axis";
            a.Cell(3,1).Value=2; a.Cell(3,2).Value="ax_1"; a.Cell(3,3).Value="AxisY"; a.Cell(3,4).Value="1.0"; a.Cell(3,5).Value=""; a.Cell(3,6).Value="beta-user"; a.Cell(3,7).Value=lm; a.Cell(3,8).Value="demo axis";

            wb.SaveAs(xlsx);
        }

        Console.WriteLine("Creato: " + xlsx);
    }
}
