using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace New_Ev
{
    internal class WB_EventArgsStatus: EventArgs
    {
        public byte[] received_buf;
        public WB_EventArgsStatus() 
        {
        }

        public WB_EventArgsStatus(byte[] received_buf)
        {
            this.received_buf = received_buf;
        }
    }
}
