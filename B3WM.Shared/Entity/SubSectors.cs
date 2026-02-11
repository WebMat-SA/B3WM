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
    [Table("SubSectors")]
    public class SubSectors
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SubSectorsID { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [ForeignKey("Sector")]
        public int SectorID { get; set; }

        [ForeignKey("SectorID")]
        public virtual Sectors Sector
        {
            get => _Sector;
            set
            {
                _Sector = value;
                if (_Sector != null) SectorID = _Sector.SectorsID;
            }
        }
        private Sectors _Sector;

        [JsonIgnore]
        public ICollection<Segments> Segments { get; set; } = new Collection<Segments>();
    }
}
