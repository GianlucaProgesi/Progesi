using System.Collections.Generic;

namespace ProgesiCore
{
  public interface IProgesiVariable
  {
    // <summary>
    /// This is Used to Create and Update a single Progesi Variable     
    /// <\summary> 
    ProgesiVariable UpdateVariable(ProgesiVariable p);
    IEnumerable<IProgesiVariable> UpdateProgesiVariables(List<ProgesiVariable> T);

    IEnumerable<IProgesiVariable> GetProgesiVariables();
    ProgesiVariable GetProgesiVariable(int id);

    void RemoveProgesiVariable(int id);

    IEnumerable<IProgesiVariable> removeProgesiVariables(List<ProgesiVariable> T);
    /// <summary>
    ///     
    /// <summary>


  }
}
