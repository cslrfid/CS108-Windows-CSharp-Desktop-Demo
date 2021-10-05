using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace CSLibrary.Structures
{
    using Constants;

    /// <summary>
    /// Write PC structures, configure this before write new PC value
    /// </summary>
    public class TagWritePcParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
#if oldcode
        /// <summary>
        /// Number of retrial will retry if write failure (Process Retry / Library Retry)
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// Number of retrial will retry if write failure (Write Retry / Firmware Retry)
        /// </summary>
        public UInt32 writeRetryCount = 32;
#endif
        /// <summary>
        /// A new pc to the 16-bit values to write to the tag's memory bank.
        /// </summary>
//        public S_PC pc = new S_PC();
        public UInt16 pc;
        /// <summary>
        /// Flag - Normal or combination of  Select or Post-Match
        /// </summary>
        public SelectFlags flags = SelectFlags.SELECT;
    }
/*
    /// <summary>
    /// Write PC structures, configure this before write new PC value
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class TagWritePcParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
        /// <summary>
        /// Number of retrial will retry if write failure
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// A new pc to the 16-bit values to write to the tag's memory bank.
        /// </summary>
        public UInt16 pc;
        /// <summary>
        /// 
        /// </summary>
        public TagWritePcParms()
        {
            // NOP
        }
    }
 */
    /// <summary>
    /// Write EPC structures, configure this before write new EPC value
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class TagWriteEpcParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
#if oldcode
        /// <summary>
        /// Number of retrial will retry if write failure (Process Retry / Library Retry)
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// Number of retrial will retry if write failure (Write Retry / Firmware Retry)
        /// </summary>
        public UInt32 writeRetryCount = 32;
#endif
        /// <summary>
        /// The offset, in the memory bank, of the first 16-bit word to write.
        /// </summary>
        public UInt16 offset;
        /// <summary>
        /// The number of 16-bit words that will be written.  This field must be
        /// between 1 and 31, inclusive.  
        /// </summary>
        public UInt16 count;
        /// <summary>
        /// A new epc to the 16-bit values to write to the tag's memory bank.
        /// </summary>
        public S_EPC epc;
        /// <summary>
        /// 
        /// </summary>
        public TagWriteEpcParms()
        {
            // NOP
        }
    }
    /// <summary>
    /// Write password structures, configure this before write new password value
    /// </summary>
    public class TagWritePwdParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
#if oldcode
        /// <summary>
        /// Number of retrial will retry if write failure (Process Retry / Library Retry)
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// Number of retrial will retry if write failure (Write Retry / Firmware Retry)
        /// </summary>
        public UInt32 writeRetryCount = 32;
#endif
        /// <summary>
        /// A new password to the 32-bit values to write to the tag's memory bank.
        /// </summary>
        //public S_PWD password = new S_PWD();
        public UInt32 password;
        /// <summary>
        /// Flag - Normal or combination of  Select or Post-Match
        /// </summary>
        public SelectFlags flags = SelectFlags.SELECT;
    }

/*    
    /// <summary>
    /// Write password structures, configure this before write new password value
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class TagWritePwdParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
        /// <summary>
        /// Number of retrial will retry if write failure
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// A new password to the 32-bit values to write to the tag's memory bank.
        /// </summary>
        public uint password;
        /// <summary>
        /// 
        /// </summary>
        public TagWritePwdParms()
        {
            // NOP
        }
    }
*/

    /// <summary>
    /// Write User structures, configure this before write new user data
    /// </summary>
    public class TagWriteUserParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
#if oldcode
        /// <summary>
        /// Number of retrial will retry if write failure (Process Retry / Library Retry)
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// Number of retrial will retry if write failure (Write Retry / Firmware Retry)
        /// </summary>
        public UInt32 writeRetryCount = 32;
#endif
        /// <summary>
        /// The offset, in the memory bank, of the first 16-bit word to write.
        /// </summary>
        public UInt16 offset;
        /// <summary>
        /// The number of 16-bit words that will be written.  
        /// </summary>                                       
        public UInt16 count;
        /// <summary>
        /// A array to the 16-bit values to write to the tag's memory bank.
        /// </summary>
        public UInt16[] pData = new UInt16[0];
        //public S_DATA pData = new S_DATA();
        /// <summary>
        /// Flag - Normal or combination of  Select or Post-Match
        /// </summary>
        public SelectFlags flags = SelectFlags.SELECT;




    }

/*
 * /// <summary>
    /// Write User structures, configure this before write new user data
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class TagWriteUserParms
    {
        /// <summary>
        /// The access password for the tags.  A value of zero indicates no 
        /// access password. 
        /// </summary>
        public UInt32 accessPassword;
        /// <summary>
        /// Number of retrial will retry if write failure
        /// </summary>
        public UInt32 retryCount;
        /// <summary>
        /// The offset, in the memory bank, of the first 16-bit word to write.
        /// </summary>
        public UInt16 offset;
        /// <summary>
        /// The number of 16-bit words that will be written.  
        /// </summary>                                       
        public UInt16 count;
        /// <summary>
        /// A array to the 16-bit values to write to the tag's memory bank.
        /// </summary>
        public UInt16[] pData = new UInt16[0];
        /// <summary>
        /// Constructor
        /// </summary>
        public TagWriteUserParms()
        {
            // NOP
        }
    }
*/
}
