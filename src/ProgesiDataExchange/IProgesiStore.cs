using System.Collections.Generic;

namespace Progesi.DataExchange
{
  public interface IProgesiStore
  {
    // READ
    IReadOnlyList<ProgesiVariableDto> GetAllVariables();
    IReadOnlyList<ProgesiMetadataDto> GetAllMetadata();
    IReadOnlyList<ProgesiAxisVariableDto> GetAllAxisVariables();

    // UPSERT (ritorna inserted, updated, skipped)
    (int inserted, int updated, int skipped) UpsertVariables(IEnumerable<ProgesiVariableDto> items);
    (int inserted, int updated, int skipped) UpsertMetadata(IEnumerable<ProgesiMetadataDto> items);
    (int inserted, int updated, int skipped) UpsertAxisVariables(IEnumerable<ProgesiAxisVariableDto> items);
  }
}
