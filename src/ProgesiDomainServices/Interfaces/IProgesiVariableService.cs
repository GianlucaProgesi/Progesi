using System;

using Progesi.DomainServices.Models;

namespace Progesi.DomainServices.Interfaces
{
  public interface IProgesiVariableService
  {
    ProgesiVariable CreateOrUpdate(ProgesiVariable input);
    bool Delete(Guid id);
    ProgesiVariable? GetById(Guid id);
    ProgesiVariable? GetByName(string name);
  }
}
