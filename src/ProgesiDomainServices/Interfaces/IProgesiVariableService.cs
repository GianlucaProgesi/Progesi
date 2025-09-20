using System;
using Progesi.DomainServices.Models;

namespace Progesi.DomainServices.Interfaces
{
  public interface IProgesiVariableService
  {
    ProgesiVariable CreateOrUpdate(ProgesiVariable v);
    ProgesiVariable GetById(Guid id);
    ProgesiVariable GetByName(string name);
    bool Delete(Guid id);
  }
}
