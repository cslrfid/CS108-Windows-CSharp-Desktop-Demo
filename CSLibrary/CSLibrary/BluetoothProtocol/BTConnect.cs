using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public class GATT
    {
        enum RESULT
        {
            SUCCESS,
            FAIL,
            READYCONNECTED,
        }


        public GATT()
        {
        }

        ~GATT ()
        {

        }

        public bool Disconnect ()
        {
            return false;
        }
    }
}
