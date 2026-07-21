using FluentAssertions;
using Xunit;

namespace Progesi.GhExcelReadContract.Tests
{
  public class GhExcelVariableValueSupportTests
  {
    [Theory]
    [InlineData("string", true)]
    [InlineData("int", true)]
    [InlineData("double", true)]
    [InlineData("Rhino.Geometry.Point3d, RhinoCommon", false)]
    [InlineData("System.Guid", false)]
    public void IsPrimitiveValueType_Classifies_Known_Types(string valueType, bool expectedPrimitive)
    {
      GhExcelVariableValueSupport.IsPrimitiveValueType(valueType).Should().Be(expectedPrimitive);
    }

    [Fact]
    public void FormatExportValue_Preserves_Primitive_String()
    {
      GhExcelVariableValueSupport.FormatExportValue("string", "12.5", "12.5")
        .Should().Be("12.5");
    }

    [Fact]
    public void FormatExportValue_Marks_Rhino_Object_Type_As_Unsupported()
    {
      GhExcelVariableValueSupport.FormatExportValue(
          "Rhino.Geometry.Point3d, RhinoCommon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=...",
          "{\"X\":1}",
          "1")
        .Should().Be("@UNSUPPORTED:RhinoObject");
    }

    [Fact]
    public void FormatExportValue_Marks_Generic_NonPrimitive_As_Unsupported()
    {
      GhExcelVariableValueSupport.FormatExportValue("System.Guid", "abc", "abc")
        .Should().Be("@UNSUPPORTED:NonPrimitive");
    }

    [Theory]
    [InlineData("42")]
    [InlineData("hello")]
    [InlineData("true")]
    [InlineData("Pippo")]
    public void IsImportSupported_Accepts_Primitive_Strings(string value)
    {
      GhExcelVariableValueSupport.IsImportSupported(value, out var kind, out _)
        .Should().BeTrue();
      kind.Should().Be(GhExcelVariableValueKind.Primitive);
    }

    [Fact]
    public void IsImportSupported_Rejects_Unsupported_Marker()
    {
      GhExcelVariableValueSupport.IsImportSupported("@UNSUPPORTED:RhinoObject", out var kind, out var detail)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.UnsupportedMarker);
      detail.Should().Be("RhinoObject");
    }

    [Theory]
    [InlineData("{\"X\":1}")]
    [InlineData("[1,2,3]")]
    public void IsImportSupported_Rejects_Json_Like_Values(string value)
    {
      GhExcelVariableValueSupport.IsImportSupported(value, out var kind, out _)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.NonPrimitiveJsonLeak);
    }

    [Theory]
    [InlineData("Referenced Brep")]
    [InlineData("Referenced Planar Curve")]
    [InlineData("Referenced Curve")]
    [InlineData("Referenced Mesh")]
    [InlineData("Referenced Surface")]
    [InlineData("Referenced Point")]
    public void FormatExportValue_Marks_Known_Rhino_Reference_Placeholders_As_Unsupported(string value)
    {
      GhExcelVariableValueSupport.FormatExportValue("string", value, value)
        .Should().Be("@UNSUPPORTED:RhinoObject");
    }

    [Theory]
    [InlineData("Referenced document")]
    [InlineData("Referenced documentation")]
    [InlineData("A long user description about referenced items")]
    [InlineData("Pippo")]
    public void FormatExportValue_Preserves_Ordinary_User_Strings(string value)
    {
      GhExcelVariableValueSupport.FormatExportValue("string", value, value)
        .Should().Be(value);
    }

    [Theory]
    [InlineData("Referenced Brep")]
    [InlineData("Referenced Planar Curve")]
    [InlineData("referenced brep")]
    public void IsKnownRhinoReferenceDisplayValue_Detects_Manual_Smoke_And_Legacy_Values(string value)
    {
      GhExcelVariableValueSupport.IsKnownRhinoReferenceDisplayValue(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("Referenced document")]
    [InlineData("Referenced documentation")]
    [InlineData("hello")]
    [InlineData("Referenced something custom")]
    public void IsKnownRhinoReferenceDisplayValue_Rejects_Non_Placeholder_Strings(string value)
    {
      GhExcelVariableValueSupport.IsKnownRhinoReferenceDisplayValue(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("Referenced Brep")]
    [InlineData("Referenced Planar Curve")]
    public void IsImportSupported_Rejects_Legacy_Rhino_Reference_Placeholders(string value)
    {
      GhExcelVariableValueSupport.IsImportSupported(value, out var kind, out var detail)
        .Should().BeFalse();
      kind.Should().Be(GhExcelVariableValueKind.ReferencedObjectDisplayString);
      detail.Should().Be("RhinoObject");
    }

    [Fact]
    public void RequiresUnsupportedExportHandling_Includes_Known_Placeholder_On_Primitive_Type()
    {
      GhExcelVariableValueSupport.RequiresUnsupportedExportHandling("string", "Referenced Brep")
        .Should().BeTrue();
      GhExcelVariableValueSupport.RequiresUnsupportedExportHandling("string", "12.5")
        .Should().BeFalse();
      GhExcelVariableValueSupport.RequiresUnsupportedExportHandling("string", "Referenced document")
        .Should().BeFalse();
    }

    [Fact]
    public void BuildImportSkipMessage_Includes_Row_And_Kind()
    {
      GhExcelVariableValueSupport.BuildImportSkipMessage(9, GhExcelVariableValueKind.UnsupportedMarker, "RhinoObject")
        .Should().Contain("R9")
        .And.Contain("RhinoObject");
    }
  }
}
