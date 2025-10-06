using System;
using System.Collections.Generic;
using Progesi.DataExchange;

namespace Progesi.GrasshopperAssembly.Components
{
  public static class RhinoBridge
  {
    private static IProgesiStore? _store; // nullable

    public static void SetHandlers(
      Func<IReadOnlyList<ProgesiVariableDto>> getVars,
      Func<IReadOnlyList<ProgesiMetadataDto>> getMets,
      Func<IReadOnlyList<ProgesiAxisVariableDto>> getAxis,
      Func<IEnumerable<ProgesiVariableDto>, (int ins, int upd, int skip)> upsertVars,
      Func<IEnumerable<ProgesiMetadataDto>, (int ins, int upd, int skip)> upsertMets,
      Func<IEnumerable<ProgesiAxisVariableDto>, (int ins, int upd, int skip)> upsertAxis)
    {
      _store = new DelegatingStore(getVars, getMets, getAxis, upsertVars, upsertMets, upsertAxis);
    }

    public static IProgesiStore? GetRhinoStore() => _store;

    private sealed class DelegatingStore : IProgesiStore
    {
      private readonly Func<IReadOnlyList<ProgesiVariableDto>> _gV;
      private readonly Func<IReadOnlyList<ProgesiMetadataDto>> _gM;
      private readonly Func<IReadOnlyList<ProgesiAxisVariableDto>> _gA;
      private readonly Func<IEnumerable<ProgesiVariableDto>, (int, int, int)> _uV;
      private readonly Func<IEnumerable<ProgesiMetadataDto>, (int, int, int)> _uM;
      private readonly Func<IEnumerable<ProgesiAxisVariableDto>, (int, int, int)> _uA;

      public DelegatingStore(
        Func<IReadOnlyList<ProgesiVariableDto>> gV,
        Func<IReadOnlyList<ProgesiMetadataDto>> gM,
        Func<IReadOnlyList<ProgesiAxisVariableDto>> gA,
        Func<IEnumerable<ProgesiVariableDto>, (int, int, int)> uV,
        Func<IEnumerable<ProgesiMetadataDto>, (int, int, int)> uM,
        Func<IEnumerable<ProgesiAxisVariableDto>, (int, int, int)> uA)
      { _gV = gV; _gM = gM; _gA = gA; _uV = uV; _uM = uM; _uA = uA; }

      public IReadOnlyList<ProgesiVariableDto> GetAllVariables() => _gV();
      public IReadOnlyList<ProgesiMetadataDto> GetAllMetadata() => _gM();
      public IReadOnlyList<ProgesiAxisVariableDto> GetAllAxisVariables() => _gA();

      public (int inserted, int updated, int skipped) UpsertVariables(IEnumerable<ProgesiVariableDto> items) => _uV(items);
      public (int inserted, int updated, int skipped) UpsertMetadata(IEnumerable<ProgesiMetadataDto> items) => _uM(items);
      public (int inserted, int updated, int skipped) UpsertAxisVariables(IEnumerable<ProgesiAxisVariableDto> items) => _uA(items);
    }
  }
}
