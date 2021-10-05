using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public partial class SiliconLabIC
    {
        public class Constants
        {
            /// <summary>
            /// SiliconLab IC Callback Type
            /// </summary>
            public enum AccessCompletedCallbackType
            {
                SERIALNUMBER,
                UNKNOWN
            }
        }

        public class Events
        {
            public class OnAccessCompletedEventArgs : EventArgs
            {
                public readonly object info;
                public readonly Constants.AccessCompletedCallbackType type = Constants.AccessCompletedCallbackType.UNKNOWN;

                public OnAccessCompletedEventArgs(object info, Constants.AccessCompletedCallbackType type)
                {
                    this.info = info;
                    this.type = type;
                }
            }
        }
    }
}
