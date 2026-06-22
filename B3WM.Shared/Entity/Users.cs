using B3WM.Shared.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    [Table("Users")]
    public class Users
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UsersID { get; set; }

        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        public string Email { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(128)]
        public string Password
        {
            get
            {
                if (EncryptedPassword != null)
                    return EncryptedPassword;
                else
                    return _Password;
            }
            set
            {
                _Password = value;
            }
        }

        [Required(AllowEmptyStrings = false)]
        [StringLength(16)]
        public string Phone { get; set; }

        [Required]
        [JsonIgnore]
        public bool IsActive { get; set; }

        [Required]
        [JsonIgnore]
        public bool NeedsConfirmation { get; set; }

        [Required]
        public bool IsAdmin { get; set; }

        private string _Password { get; set; }

        [Required]
        public string FullName { get; set; }

        [NotMapped]
        [JsonIgnore]
        public string FirstName => char.ToUpper((FullName?.Trim().Split(" ")[0])[0]) + (FullName?.Trim().Split(" ")[0]).Substring(1).ToLower();

        public ICollection<Credentials> Credentials { get; set; } = new Collection<Credentials>();
        public ICollection<Alerts> Alerts { get; set; } = new Collection<Alerts>();
        public ICollection<Reminders> Reminders { get; set; } = new Collection<Reminders>();
        //public ICollection<Setups> Setups { get; set; } = new Collection<Setups>();

        [JsonIgnore]
        public ICollection<Links> Links { get; set; } = new Collection<Links>();

        [JsonIgnore]
        [ForeignKey("Plan")]
        public int? PlanID { get; set; }
        [ForeignKey("PlanID")]
        public virtual Plans Plan
        {
            get => _Plan;
            set
            {
                _Plan = value;
                if (_Plan != null) 
                    PlanID = _Plan.PlansID;
                else 
                    PlanID = null;
            }
        }
        private Plans _Plan;


        [NotMapped]
        [JsonIgnore]
        [Display(Name = "Senha criptografada")]
        public string EncryptedPassword { get => _EncryptedPassword; }

        private string _EncryptedPassword { get; set; }

        public void EncryptPassword()
        {
            //para pegar o password correto
            _EncryptedPassword = null;

            //array de string password em bytes
            byte[] data = Encoding.ASCII.GetBytes(Password);

            //popoula o encrypted com uma string ja criptografada
            _EncryptedPassword = BitConverter.ToString(new SHA512Managed().ComputeHash(data)).Replace("-", "").ToUpper();
        }


        [JsonIgnore]
        public DateTime? LastLogin { get; set; }
    }
}