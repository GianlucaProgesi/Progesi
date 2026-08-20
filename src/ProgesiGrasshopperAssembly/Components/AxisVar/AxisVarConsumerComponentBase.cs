#nullable enable
using Grasshopper.Kernel;
using ProgesiGrasshopperAssembly.Infrastructure.AxisVar;

namespace ProgesiGrasshopperAssembly.Components.AxisVar
{
  /// <summary>
  /// Base for axis-consuming GH components whose optional Generic Axis input must not
  /// block SolveInstance when unwired (Id-only load path).
  /// </summary>
  public abstract class AxisVarConsumerComponentBase : GH_Component
  {
    protected AxisVarConsumerComponentBase(
      string name,
      string abbreviation,
      string description,
      string category,
      string subCategory)
      : base(name, abbreviation, description, category, subCategory)
    { }

    protected virtual int OptionalAxisInputIndex => 1;

    protected override void BeforeSolveInstance()
    {
      AxisVarGhSupport.PrepareOptionalAxisInput(this, OptionalAxisInputIndex);
      base.BeforeSolveInstance();
    }
  }
}
