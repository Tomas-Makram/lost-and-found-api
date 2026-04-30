using DataLayer.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessLayer.DTOs
{
    public class MyAccountDTO
    {
        public Guid UserId { get; set; } = Guid.NewGuid();

        public string UserProfileImgURL { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string NationalId { get; set; } = string.Empty;


        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string Address { get; set; } = string.Empty;

        // Account Information
        public string AccountType { get; set; } = "User";
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public bool Verified { get; set; } = false;
        public bool Login { get; set; } = false;
    }
}