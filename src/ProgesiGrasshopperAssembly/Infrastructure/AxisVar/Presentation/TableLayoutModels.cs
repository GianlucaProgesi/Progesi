using System.Collections.Generic;

namespace ProgesiGrasshopperAssembly.Infrastructure.AxisVar.Presentation
{
  public sealed class TableCellSpec
  {
    public TableCellSpec(int row, int column, string text, bool isHeader)
    {
      Row = row;
      Column = column;
      Text = text ?? string.Empty;
      IsHeader = isHeader;
    }

    public int Row { get; }
    public int Column { get; }
    public string Text { get; }
    public bool IsHeader { get; }
  }

  public sealed class TableLayout
  {
    public TableLayout(
      LayoutPoint2d origin,
      double width,
      double height,
      int rowCount,
      int columnCount,
      IReadOnlyList<TableCellSpec> cells,
      string objectIdsCsv)
    {
      Origin = origin;
      Width = width;
      Height = height;
      RowCount = rowCount;
      ColumnCount = columnCount;
      Cells = cells;
      ObjectIdsCsv = objectIdsCsv ?? string.Empty;
    }

    public LayoutPoint2d Origin { get; }
    public double Width { get; }
    public double Height { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public IReadOnlyList<TableCellSpec> Cells { get; }
    public string ObjectIdsCsv { get; }
  }
}
