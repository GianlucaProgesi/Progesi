using FluentAssertions;
using ProgesiCore;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;
using Xunit;

namespace ProgesiRepositories.Rhino.Tests.AxisVar
{
  public sealed class AxisVarInterpolateStepTests
  {
    [Fact]
    public void EvaluateStepValue_StringType_Returns_Nearest_Keypoint_Label()
    {
      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Material",
        "System.String",
        10.0,
        keyPoints: new[] { 0.0, 1.0 });

      axis.SetLabel(0.0, "A");
      axis.SetLabel(1.0, "B");

      AxisVarGhSupport.EvaluateStepValue(axis, 0.4).Should().Be("A");
      AxisVarGhSupport.EvaluateStepValue(axis, 0.0).Should().Be("A");
      AxisVarGhSupport.EvaluateStepValue(axis, 1.0).Should().Be("B");
      AxisVarGhSupport.EvaluateStepValue(axis, 0.999).Should().Be("A");
    }

    [Fact]
    public void EvaluateInterpolateValue_NonEmbeddedRef_FallsBack_To_StepValue_NeverNull()
    {
      var axis = new ProgesiAxisVariable(
        1,
        "Axis-A",
        "Thickness",
        "System.Double",
        10.0,
        keyPoints: new[] { 0.0, 1.0 },
        functionRef: ProgesiFunctionRef.ByHashtag("missing-fn"));

      axis.SetLabel(0.0, "42");

      var (value, info) = AxisVarGhSupport.EvaluateInterpolateValue(axis, 0.4);

      value.Should().NotBeNull();
      value.Should().Be("42");
      info.Should().Contain("not embedded");
    }
  }
}
