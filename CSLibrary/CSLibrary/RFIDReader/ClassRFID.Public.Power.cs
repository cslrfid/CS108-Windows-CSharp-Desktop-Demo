using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CSLibrary.Constants;

namespace CSLibrary
{
    public partial class RFIDReader
    {
        /// <summary>
        /// Available Maximum Power you can set on specific region
        /// </summary>
        public uint GetActiveMaxPowerLevel()
        {
            return 320;
        }

        /// <summary>
        /// Get Power Level
        /// </summary>
        public Result GetPowerLevel(ref uint pwrlvl)
        {
            MacWriteRegister(MACREGISTER.HST_ANT_DESC_SEL, 0);
            MacReadRegister(MACREGISTER.HST_ANT_DESC_RFPOWER, ref pwrlvl);

            return Result.OK;
        }

        public void SetPowerLevel(UInt32 pwrlevel)
        {
            MacWriteRegister(MACREGISTER.HST_ANT_DESC_SEL, 0);         // select antenna
            MacWriteRegister(MACREGISTER.HST_ANT_DESC_RFPOWER, pwrlevel);
        }

    }
}
