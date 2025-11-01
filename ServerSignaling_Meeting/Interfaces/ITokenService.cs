using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerSignaling_Meeting.Interfaces
{
    public interface ITokenService
    {
        string CreateToken(AppUser user);
    }
}
