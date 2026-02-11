using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace B3WM.Shared.Entity
{
    [Table("SetupsCustomers")]
    public class SetupsCustomers
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SetupsCustomersID { get; set; }

        [Required]
        public string Symbol { get; set; }

        [Required, ForeignKey("User")]
        public int UserID { get; set; }

        [JsonIgnore]
        [ForeignKey("UserID")]
        public virtual Users User { get; set; }
    }
}
