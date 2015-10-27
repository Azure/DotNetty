using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Codecs.CoapTcp
{
    public enum ResponseCode
    {
        Created,
        Deleted,
        Valid,
        Changed,

        Content,
        Bad_Request,
        Unauthorized,
        Bad_Option,
        Forbidden,
        NotFound,        
        Methd_Not_Allowed,        
        Not_Acceptable,
        Precondition_Failed,
        Request_Entity_Too_Large,
        Unsupported_Content_Format,
        
        Internal_Server_Error,
        Not_Implemented,
        Bad_Gateway,
        Service_Unavailable,
        Gateway_Timeout,
        Proxying_Not_Supported,
    }
}
