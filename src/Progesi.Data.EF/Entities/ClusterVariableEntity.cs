using System.ComponentModel.DataAnnotations.Schema;

namespace Progesi.Data.EF.Entities
{
  [Table("ClusterVariables")]
  public class ClusterVariableEntity
  {
    public int ClusterId { get; set; }
    public int VarId { get; set; }

    public virtual ClusterEntity Cluster { get; set; }

    // opzionale: collega anche VariableEntity se esiste
    // public virtual VariableEntity Variable { get; set; }
  }
}
