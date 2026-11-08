using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ASI.Basecode.Resources.Constants.Enums;

namespace ASI.Basecode.Services.ServiceModels
{
    public class LoginResponse
    {
        public LoginResult Result { get; set; }
        public LoginFailureReason? FailureReason { get; set; }
        public Account Account { get; set; }
    }
}
