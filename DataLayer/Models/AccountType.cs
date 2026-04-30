using System.ComponentModel.DataAnnotations;

namespace DataLayer.Models
{
    enum AccountType
    {
        [Display(Name = "Customer")]
        Customer = 1,
        
        [Display(Name = "Service Provider")]
        ServiceProvider = 2,
        
        [Display(Name = "Admin")]
        Admin = 3,

        [Display(Name = "Helper")]
        Helper = 4
    }
}