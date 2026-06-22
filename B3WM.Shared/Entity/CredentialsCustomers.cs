using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json.Serialization;

namespace B3WM.Shared.Entity
{
    public class CredentialsCustomers
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CredentialsCustomersID { get; set; }

        [JsonIgnore]
        [Required]
        [ForeignKey("Credential")]

        public int CredentialID { get; set; }

        [JsonIgnore]
        [ForeignKey("CredentialID")]
        public virtual Credentials Credential 
        {
            get => _Credential;
            set
            {
                _Credential = value;
                if (_Credential != null) CredentialID = _Credential.CredentialsID;
            }
        }
        private Credentials _Credential;

        [JsonIgnore]
        [Required]
        [ForeignKey("Customer")]
        public int CustomerID { get; set; }
        [ForeignKey("CustomerID")]
        public virtual Customers Customer 
        {
            get => _Customer;
            set
            {
                _Customer = value;
                if (_Customer != null) CustomerID = _Customer.CustomersID;
            }
        }
        private Customers _Customer;

    }
}
