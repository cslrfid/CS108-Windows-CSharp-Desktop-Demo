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
        /// If true, it can only set to hopping channel.
        /// </summary>
        public bool IsHoppingChannelOnly
        {
            get { return m_oem_freq_modification_flag != 0x00; }
        }

        /// <summary>
        /// If true, it can only set to fixed channel.
        /// Otherwise, both fixed and hopping can be set.
        /// </summary>
        public bool IsFixedChannelOnly
        {
            get { return (m_save_country_code == 1 | m_save_country_code == 3 | m_save_country_code == 8 | m_save_country_code == 9); }
        }

        /// <summary>
        /// Get Fixed frequency channel
        /// </summary>
        public bool IsFixedChannel
        {
            get { { return m_save_fixed_channel; } }
        }

        /// <summary>
        /// GetCountryCode
        /// </summary>
        /// <returns>Result</returns>
        public Result GetCountryCode(ref uint code)
        {
            code = m_save_country_code;

            if (code < 0 || code > 8)
                return Result.INVALID_OEM_COUNTRY_CODE;

            return Result.OK;
        }

        /// <summary>
        /// Available region you can use
        /// </summary>
        public List<RegionCode> GetActiveRegionCode()
        {
            //DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.GetActiveRegionCode()");

            return m_save_country_list;
        }

        /// <summary>
        /// Get Region Profile
        /// </summary>
        public RegionCode SelectedRegionCode
        {
            get { return m_save_region_code; }
        }
    }
}
