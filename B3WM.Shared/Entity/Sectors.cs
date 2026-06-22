using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("Sectors")]
    public class Sectors
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SectorsID { get; set; }
        
        [Required]
        public string Name { get; set; }

        [JsonIgnore]
        public ICollection<SubSectors> SubSectors { get; set; } = new Collection<SubSectors>();
    }
}
