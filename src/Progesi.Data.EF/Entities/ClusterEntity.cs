using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Progesi.Data.EF.Entities
{
  [Table("Clusters")]
  public class ClusterEntity
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public string Hash { get; set; } = "";

    [Required]
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public virtual ICollection<ClusterVariableEntity> ClusterVariables { get; set; }
      = new List<ClusterVariableEntity>();
  }
}
