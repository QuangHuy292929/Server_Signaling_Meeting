using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalingServer.Controllers
{
    [Route("api/message")]
    [ApiController]
    [Authorize]
    public class MessageController :ControllerBase
    {

    }
}
