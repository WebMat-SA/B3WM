using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    [Table("Plans")]
    public class Plans
    {
        [JsonIgnore]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PlansID { get; set; }

        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public int MaxRequestsPerMinute { get; set; }

        [Required]
        public bool IsRealTime { get; set; }

        [Required]
        public int MaxRealTimePapers { get; set; }

        [Required]
        public int MaxCredentials { get; set; }


        [JsonIgnore]
        [Required]
        public bool IsDefault { get; set; }
    }
}
