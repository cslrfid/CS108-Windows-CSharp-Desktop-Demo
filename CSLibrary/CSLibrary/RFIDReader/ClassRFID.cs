using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CSLibrary;
using CSLibrary.Constants;
using CSLibrary.Structures;
using CSLibrary.Events;
using CSLibrary.Tools;

namespace CSLibrary
{
    public partial class RFIDReader
    {
        #region public variable
        #region ====================== Callback Event Handler ======================
        /// <summary>
        /// Reader Operation State Event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnStateChangedEventArgs> OnStateChanged;

        /// <summary>
        /// Tag Inventory(including Inventory and search) callback event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnAsyncCallbackEventArgs> OnAsyncCallback;

        /// <summary>
        /// Tag Access (including Tag read/write/kill/lock) completed event
        /// </summary>
        public event EventHandler<CSLibrary.Events.OnAccessCompletedEventArgs> OnAccessCompleted;

        #endregion
        #endregion

        /// <summary>
        /// CSLibrary Operation parameters
        /// Notes : you must config this parameters before perform any operation
        /// </summary>
        public CSLibraryOperationParms Options
        {
            get { return m_rdr_opt_parms; }
            set { m_rdr_opt_parms = value; }
        }
        public UInt32 LastMacErrorCode;


        private HighLevelInterface _deviceHandler;
        private CSLibrary.Tools.Queue _dataBuffer = new Tools.Queue(16 * 1024 * 1024);
        //RFIDREADERCMDSTATUS _readerStatus = RFIDREADERCMDSTATUS.IDLE;
        private RFState m_state = RFState.IDLE;
        private Result m_Result;
        private Machine m_oem_machine = Machine.UNKNOWN;
        private string m_PCBAssemblyCode;

        /// <summary>
        /// Current Operation State
        /// </summary>
        public RFState State
        {
            get { { return m_state; } }
            private set { { m_state = value; } }
        }

        public void ClearEventHandler()
        {
            //OnStateChanged = delegate { };
            //OnAsyncCallback = delegate { };
            //OnAccessCompleted = delegate { };
            OnStateChanged = null;
            OnAsyncCallback = null;
            OnAccessCompleted = null;
        }

        public ChipSetID OEMChipSetID
        {
            get
            {
                return ChipSetID.R2000;

                switch (m_oem_machine)
                {
                    case Machine.CS103:
                    case Machine.CS108:
                    case Machine.CS209:
                    case Machine.CS333:
                    case Machine.CS463:
                        return ChipSetID.R2000;
                }

                return ChipSetID.R1000;
            }
        }

        public string GetModelName()
        {
            return m_oem_machine.ToString();
        }

        public string GetCountryCode()
        {
            string country = "-" + m_save_country_code.ToString();

            if (m_save_country_code == 2)
            {
                if (m_oem_freq_modification_flag == 0)
                {
                    country += " RW";
                }
                else
                {
                    switch (m_oem_special_country_version)
                    {
                        case 0x4f464341:
                            country += " OFCA";
                            break;
                        case 0x2a2a4153:
                            country += " AS";
                            break;
                        case 0x2a2a4e5a:
                            country += " NZ";
                            break;
                    }
                }
            }

            return country;
        }

        public string GetPCBAssemblyCode()
        {
            return m_PCBAssemblyCode;
        }

#if nouse
        public string GetPCBAssemblyCode()
        {
            uint[] data = new uint[4];

            try
            {
                GetOEMData(0x04, ref data[0]);
                GetOEMData(0x05, ref data[1]);
                GetOEMData(0x06, ref data[2]);
                GetOEMData(0x07, ref data[3]);
                return uint32ArrayToString(data).Replace("\0", "");

                /*
                if (MacReadOemData(0x00000004, 4, data) == Result.OK)
                {
                    return uint32ArrayToString(data).Replace("\0", "");
                }*/
            }
            catch { }

            return "Unknown";
        }
#endif

            int TwoByteArraryToInt(byte[] data, int startIndex)
        {
            int a = data[startIndex];
            a |= (data[startIndex + 1] << 8);

            return (a);
        }

        int FourByteArraryToInt(byte[] data, int startIndex)
        {
            int a = data[startIndex];
            a |= (data[startIndex + 1] << 8);
            a |= (data[startIndex + 2] << 16);
            a |= (data[startIndex + 3] << 24);

            return (a);
        }

        internal static void ArrayCopy(byte[] src, int srcOffset, UInt16[] dest, int destOffset, int byteSize)
        {
            int len = byteSize / 2;

            if ((byteSize % 2) != 0 || (src.Length - srcOffset) < byteSize || (dest.Length - destOffset) < len)
            {
                return;
                //throw new ArgumentException();
            }

            for (int cnt = 0; cnt < len; cnt++)
                dest[destOffset + cnt] = (UInt16)(src[srcOffset + cnt * 2] << 8 | src[srcOffset + cnt * 2 + 1]);
        }

        private String uint32ArrayToString(UInt32[] source)
        {
            StringBuilder sb = new StringBuilder();

            // Byte at offset is total byte len, 2nd byte is always 3

            for (int index = 0; index < source.Length; index++)
            {
                sb.Append((Char)(source[index] >> 24 & 0x000000FF));
                sb.Append((Char)(source[index] >> 16 & 0x000000FF));
                sb.Append((Char)(source[index] >> 8 & 0x000000FF));
                sb.Append((Char)(source[index] >> 0 & 0x000000FF));
            }

            return sb.ToString();
        }

        Single R2000_RssiTranslation(byte rawValue)
        {
            int iMantissa = rawValue & 0x07;
            int iExponent = (rawValue >> 3) & 0x1F;

            double dRSSI = 20.0 * Math.Log10(Math.Pow(2.0, (double)iExponent) * (1.0 + ((double)iMantissa / 8.0)));
            return (Single) dRSSI;
        }

        bool R2000Packet_NewInventory(byte[] recvData, int offset = 0)
        {
            if (OnAsyncCallback != null)
            {
                uint newInventoryPacketOffset = 8;

                while (newInventoryPacketOffset < (recvData.Length - 1))
                {
                    CSLibrary.Structures.TagCallbackInfo info = new CSLibrary.Structures.TagCallbackInfo();

                    info.pc = (UInt16)(recvData[newInventoryPacketOffset] << 8 | recvData[newInventoryPacketOffset + 1]);
                    int epcbytelen = ((info.pc & 0xf800) >> 11) * 2;
                    //info.epcstrlen = (uint)((info.pc & 0xf800) >> 11) * 4;
                    info.epcstrlen = info.epcstrlen * 2;

                    if ((newInventoryPacketOffset + epcbytelen + 1) >= recvData.Length)
                        return false;

                    info.rssi = R2000_RssiTranslation(recvData[newInventoryPacketOffset + epcbytelen + 2]);

                    byte[] byteEpc = new byte[epcbytelen];
                    Array.Copy(recvData, (int)(newInventoryPacketOffset + 2), byteEpc, 0, epcbytelen);

                    info.epc = new S_EPC(byteEpc);

                    newInventoryPacketOffset += (uint)(2 + epcbytelen + 1);

                    switch (CurrentOperation)
                    {
                        case Operation.TAG_RANGING:
                            {
                                CSLibrary.Constants.CallbackType type = CSLibrary.Constants.CallbackType.TAG_RANGING;
                                CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                                if (OnAsyncCallback != null)
                                    OnAsyncCallback(_deviceHandler, callBackData);
                            }
                            break;
                    }
                }
            }

            return true;
        }

        /*
        bool R2000Packet_NewInventory(byte[] recvData, int offset = 0)
        {
            if (OnAsyncCallback != null)
            {
                uint newInventoryPacketOffset = 8;

                while (newInventoryPacketOffset < (recvData.Length - 1))
                {
                    CSLibrary.Structures.TagCallbackInfo info = new CSLibrary.Structures.TagCallbackInfo();

                    info.pc = (UInt16)(recvData[newInventoryPacketOffset] << 8 | recvData[newInventoryPacketOffset + 1]);
                    int epcbytelen = ((info.pc & 0xf800) >> 11) * 2;
                    //info.epcstrlen = (uint)((info.pc & 0xf800) >> 11) * 4;
                    info.epcstrlen = info.epcstrlen * 2;

                    if ((newInventoryPacketOffset + epcbytelen + 1) >= recvData.Length)
                        return false;

                    info.rssi = R2000_RssiTranslation(recvData[newInventoryPacketOffset + epcbytelen + 2]);

                    byte[] byteEpc = new byte[epcbytelen];
                    Array.Copy(recvData, (int)(newInventoryPacketOffset + 2), byteEpc, 0, epcbytelen);

                    info.epc = new S_EPC(byteEpc);

                    newInventoryPacketOffset += (uint)(2 + epcbytelen + 1);

                    switch (CurrentOperation)
                    {
                        case Operation.TAG_RANGING:
                            {
                                CSLibrary.Constants.CallbackType type = CSLibrary.Constants.CallbackType.TAG_RANGING;
                                CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                                OnAsyncCallback(_deviceHandler, callBackData);
                            }
                            break;
                    }
                }
            }

            return true;
        }*/

        bool R2000Packet_NewInventory_bug(byte[] recvData, int offset = 0)
        {
            if (OnAsyncCallback != null)
            {
                uint newInventoryPacketOffset = 8;

                while (newInventoryPacketOffset < recvData.Length)
                {
                    CSLibrary.Structures.TagCallbackInfo info = new CSLibrary.Structures.TagCallbackInfo();

                    info.pc = (UInt16)(recvData[newInventoryPacketOffset] << 8 | recvData[newInventoryPacketOffset + 1]);
                    int epcbytelen = ((info.pc & 0xf800) >> 11) * 2;
                    //info.epcstrlen = (uint)((info.pc & 0xf800) >> 11) * 4;
                    info.epcstrlen = info.epcstrlen * 2;

                    if ((newInventoryPacketOffset + epcbytelen + 1) > recvData.Length)
                        return false;

                    info.rssi = R2000_RssiTranslation(recvData[newInventoryPacketOffset + epcbytelen + 2]);

                    byte[] byteEpc = new byte[epcbytelen];
                    Array.Copy(recvData, (int)(newInventoryPacketOffset + 2), byteEpc, 0, epcbytelen);

                    info.epc = new S_EPC(byteEpc);

                    newInventoryPacketOffset += (uint)(2 + epcbytelen + 1);

                    switch (CurrentOperation)
                    {
                        case Operation.TAG_RANGING:
                            {
                                CSLibrary.Constants.CallbackType type = CSLibrary.Constants.CallbackType.TAG_RANGING;
                                CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                                if (OnAsyncCallback != null)
                                    OnAsyncCallback(_deviceHandler, callBackData);
                            }
                            break;
                    }
                }
            }

            return true;
        }

        bool R2000Packet_Inventory (byte [] recvData, int offset = 0)
        {
            if (OnAsyncCallback != null)
            {
                CSLibrary.Structures.TagCallbackInfo info = new CSLibrary.Structures.TagCallbackInfo();

                var pkt_len = (UInt16)(recvData[offset + 4] | (recvData[offset + 5] << 8));
                var flags = recvData[offset + 1];

                info.ms_ctr = (UInt32)(recvData[offset + 8 + 0] | recvData[offset + 8 + 1] << 8 | recvData[offset + 8 + 2] << 16 | recvData[offset + 8 + 3] << 24);
                //info.rssi = (Single)(recvData[offset + 8 + 5] * 0.8);
                switch (OEMChipSetID)
                {
                    default:
                        info.rssi = (Single)(recvData[offset + 8 + 5] * 0.8);
                        break;

                    case ChipSetID.R2000:
                        info.rssi = R2000_RssiTranslation(recvData[offset + 8 + 5]);
                        break;
                }

                if (currentInventoryFreqRevIndex != null)
                {
                    var pseudoChannel = recvData[offset + 8 + 7];

                    info.freqChannel = (pseudoChannel < currentInventoryFreqRevIndex.Length) ? currentInventoryFreqRevIndex[pseudoChannel] : (uint)0xff;
                }

                info.antennaPort = (UInt16)(recvData[offset + 8 + 10] | recvData[offset + 8 + 11] << 8);
                info.pc = (UInt16)(recvData[offset + 8 + 12] << 8 | recvData[offset + 8 + 13]);
                info.epcstrlen = (UInt16)((((pkt_len - 3) * 4) - ((flags >> 6) & 3) - 4));
                info.crc16 = (UInt16)(recvData[offset + 8 + 14 + info.epcstrlen] << 8 | recvData[offset + 8 + 15 + info.epcstrlen]);

                {
                    var PhaseByte = recvData[offset + 8 + 6];

                    info.phase = (Int16)(PhaseByte & 0x3f);
                    if ((PhaseByte & 0x40) != 0x00)
                    {
                        UInt16 pvalue = (UInt16)info.phase;
                        pvalue |= 0xffc0;
                        info.phase = (Int16)pvalue;
                    }
                }

                if (CurrentOperation == Operation.TAG_RANGING)
                {
                    if (_tagRangingParms.multibanks == 2)
                    {
                        UInt16[] data = new UInt16[_tagRangingParms.count2];
                        int data2length = (int)(_tagRangingParms.count2 * 2);
                        int data2offset = (int)(offset + 8 + 14 + info.epcstrlen - data2length);

                        ArrayCopy(recvData, data2offset, data, 0, data2length);
                        info.Bank2Data = data;

                        CSLibrary.Debug.WriteLine("data 1:" + Tools.Hex.ToString(data));

                        info.epcstrlen -= (uint)data2length;
                    }

                    if (_tagRangingParms.multibanks > 0)
                    {
                        UInt16[] data = new UInt16[_tagRangingParms.count1];
                        int data1length = (int)(_tagRangingParms.count1 * 2);
                        int data1offset = (int)(offset + 8 + 14 + info.epcstrlen - data1length);

                        ArrayCopy(recvData, data1offset, data, 0, data1length);
                        info.Bank1Data = data;

                        CSLibrary.Debug.WriteLine("data 2:" + Tools.Hex.ToString(data));

                        info.epcstrlen -= (uint)data1length;
                    }
                }

				byte[] byteEpc = new byte[info.epcstrlen];
                Array.Copy(recvData, (int)(offset + 8 + 14), byteEpc, 0, (int)info.epcstrlen);

                info.epc = new S_EPC(byteEpc);

                switch (CurrentOperation)
                {
                    case Operation.TAG_RANGING:
                        {
                            CSLibrary.Constants.CallbackType type = CSLibrary.Constants.CallbackType.TAG_RANGING;
                            CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                            if (OnAsyncCallback != null)
                                OnAsyncCallback(_deviceHandler, callBackData);
                        }
                        break;

                    case Operation.TAG_SEARCHING:
                        {
                            bool match = true;
                            // mach tag selected
                            if (_tagSelectedParms.bank == MemoryBank.EPC && ((_tagSelectedParms.flags & SelectMaskFlags.ENABLE_PC_MASK) == 0) && _tagSelectedParms.epcMaskLength == 96)
                            {
                                byte [] data = _tagSelectedParms.epcMask.ToBytes();

                                if (data.Length != byteEpc.Length)
                                    break;

                                for (int cnt= 0; cnt < data.Length; cnt++)
                                    if (data[cnt] != byteEpc[cnt])
                                    {
                                        match = false;
                                        break;
                                    }
                            }

                            if (match)
                            {
                                CSLibrary.Constants.CallbackType type = CSLibrary.Constants.CallbackType.TAG_SEARCHING;
                                CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                                if (OnAsyncCallback != null)
                                    OnAsyncCallback(_deviceHandler, callBackData);
                            }
                            else
                            {
                                CSLibrary.Debug.WriteLine("Found a non-match Tag");
                            }
                        }
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }

        bool R2000Packet_TagAccess (byte[] recvData, int offset = 0)
        {
            if ((recvData[offset + 1] & 0x0f) != 0)
                return false;

			Operation RealCurrentOperation = (Operation)(_deviceHandler._currentCmdRemark);

			switch (recvData[offset + 12])
            {
                case 0xc2:  // Read
                    switch (RealCurrentOperation)
                    {
                        case CSLibrary.Constants.Operation.TAG_READ_PC:
                            m_rdr_opt_parms.TagReadPC.m_pc = (ushort)((recvData[offset + 20] << 8) | (recvData[offset + 21]));
                            break;

                        case CSLibrary.Constants.Operation.TAG_READ_EPC:
                            ArrayCopy(recvData, offset + 20, m_rdr_opt_parms.TagReadEPC.m_epc, 0, m_rdr_opt_parms.TagReadEPC.count * 2);
                            break;

                        case CSLibrary.Constants.Operation.TAG_READ_ACC_PWD:
                            m_rdr_opt_parms.TagReadAccPwd.m_password = (UInt32)((recvData[offset + 20] << 24) | (recvData[offset + 21] << 16) | (recvData[offset + 22] << 8) | (recvData[offset + 23]));
                            break;

                        case CSLibrary.Constants.Operation.TAG_READ_KILL_PWD:
							m_rdr_opt_parms.TagReadKillPwd.m_password = (UInt32)((recvData[offset + 20] << 24) | (recvData[offset + 21] << 16) | (recvData[offset + 22] << 8) | (recvData[offset + 23]));
							break;

                        case CSLibrary.Constants.Operation.TAG_READ_TID:
                            ArrayCopy(recvData, offset + 20, m_rdr_opt_parms.TagReadTid.pData, 0, m_rdr_opt_parms.TagReadTid.count * 2);
                            break;

                        case CSLibrary.Constants.Operation.TAG_READ_USER:
                            ArrayCopy(recvData, offset + 20, m_rdr_opt_parms.TagReadUser.m_pData, 0, m_rdr_opt_parms.TagReadUser.count * 2);
                            break;
                    }
                    break;

                case 0xc3:  // Write
					switch (RealCurrentOperation)
					{
						case CSLibrary.Constants.Operation.TAG_WRITE_PC:
							//m_rdr_opt_parms.TagReadPC.m_pc = (ushort)((recvData[offset + 20] << 8) | (recvData[offset + 21]));
							break;

						case CSLibrary.Constants.Operation.TAG_WRITE_EPC:
							//ArrayCopy(recvData, offset + 20, m_rdr_opt_parms.TagReadEPC.m_epc, 0, m_rdr_opt_parms.TagReadEPC.count * 2);
							break;

						case CSLibrary.Constants.Operation.TAG_WRITE_ACC_PWD:
							//m_rdr_opt_parms.TagReadAccPwd.m_password = (UInt32)((recvData[offset + 20] << 24) | (recvData[offset + 21] << 16) | (recvData[offset + 22] << 8) | (recvData[offset + 23]));
							break;

						case CSLibrary.Constants.Operation.TAG_WRITE_KILL_PWD:
							//m_rdr_opt_parms.TagReadKillPwd.m_password = (UInt32)((recvData[offset + 20] << 24) | (recvData[offset + 21] << 16) | (recvData[offset + 22] << 8) | (recvData[offset + 23]));
							break;

						case CSLibrary.Constants.Operation.TAG_WRITE_USER:
							//ArrayCopy(recvData, offset + 20, m_rdr_opt_parms.TagReadUser.m_pData, 0, m_rdr_opt_parms.TagReadEPC.count * 2);
							break;
					}
					break;

				case 0xc4:  // Kill
                    break;

                case 0xc5:  // Lock
							/*
							 *
							 * 
								Win32.memcpy(tagreadbuf, TagAccessPacket, 20, (uint)len);

 * 							 * if (m_Result == Result.OK && !Options.TagBlockLock.setPermalock)
										{
											Options.TagBlockLock.mask = new ushort[Options.TagBlockLock.count];
											Array.Copy(tagreadbuf, Options.TagBlockLock.mask, Options.TagBlockLock.count);
										}
							*/
					break;

                case 0x04:  // EAS
                    break;

                default:
                    return false;
            }

            return true;
        }

        public void ClearBuffer()
        {
            _dataBuffer.Clear();
        }


#if !oldcode
		/// <summary>
		/// Transfer BT API packet to R2000 packet
		/// </summary>
		/// <param name="recvData"></param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		internal CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE DeviceRecvData(byte[] recvData1, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE currentCommandResponse)
		{
			if (!_dataBuffer.DataIn(recvData1, 10, recvData1[2] - 2))
                CSLibrary.Debug.WriteLine("RFID ring buffer FULL!!!!");
			CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE result = HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.NOWAIT;

			while (_dataBuffer.length >= 8 )
			{
				byte[] recvData = _dataBuffer.DataPreOut(8);

				// first packet
				byte header = recvData[0];

				switch (header)
				{
                    default:
                        _dataBuffer.Clear();
                        CSLibrary.Debug.WriteLine("Can not handle R2000 packet type :0x", header.ToString("X2"));
                        break;
                    case 0x40:  // Abort packet
						{
							//result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA2 | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.BTAPIRESPONSE;
                            result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA2;
                            result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.BTAPIRESPONSE;
                            result = result | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA2 | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.BTAPIRESPONSE;
                            _dataBuffer.DataDel(8);
                            LastMacErrorCode = 0x0000;
                            FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                        }
                        break;

					case 0x00:
					case 0x70:  // register read packet
						{
							UInt16 add = (UInt16)(recvData[3] << 8 | recvData[2]);
							UInt32 data = (UInt32)(recvData[7] << 24 | recvData[6] << 16 | recvData[5] << 8 | recvData[4]);

                            SaveMacRegister(add, data);

                            result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;

							_dataBuffer.DataDel(8);
						}
						break;

                    case 0x04:  // New Inventory Packet

                        //CSLibrary.HighLevelInterface._debugBLEHold = true;

                        // only valid on inventory compact mode
                        //if ((Operation)(_deviceHandler._currentCmdRemark) == Operation.TAG_RANGING)
                        {
                            int pkt_type = BitConverter.ToUInt16(recvData, 2) & 0x7fff;
                            int packetLen = BitConverter.ToUInt16(recvData, 4) + 8;

                            if (packetLen > _dataBuffer.length)
                                return result;

                            switch (pkt_type)
                            {
                                case 0x0005:    /// inventory packet
									{
                                        InventoryDebug.InventoryPackerCountInc();

                                        byte[] packetData = _dataBuffer.DataOut(packetLen);

                                        R2000Packet_NewInventory(packetData);
                                    }
                                    break;
                            }
                        }
                        break;

					    case 0x01:
                        case 0x02:
                        case 0x03:
                        {
                            int pkt_type = BitConverter.ToUInt16(recvData, 2) & 0x7fff;
							int packetLen = BitConverter.ToUInt16(recvData, 4) * 4 + 8;

							if (packetLen > _dataBuffer.length)
								return result;

							switch (pkt_type)
							{
								default:
									_dataBuffer.DataDel(packetLen);
									break;

								case 0x0000:    // Command begin Packet
									_dataBuffer.DataDel(packetLen);
									break;

								case 0x0001:    // Command end Packet
									result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.COMMANDENDRESPONSE;
                                    //_dataBuffer.DataDel(packetLen);
                                    {
                                        byte[] packetData = _dataBuffer.DataOut(packetLen);
                                        LastMacErrorCode = BitConverter.ToUInt32(packetData, 12);
                                    }

                                    // Check Tag access packet
                                    // if ((currentCommandResponse | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0)
                                    {
										Operation RealCurrentOperation = (Operation)(_deviceHandler._currentCmdRemark);

										switch (RealCurrentOperation)
										{
											case CSLibrary.Constants.Operation.TAG_READ_PC:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.PC,
														CSLibrary.Constants.TagAccess.READ,
														m_rdr_opt_parms.TagReadPC.pc));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_READ_EPC:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														Bank.EPC,
														TagAccess.READ,
														m_rdr_opt_parms.TagReadEPC.epc));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_READ_ACC_PWD:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.ACC_PWD,
														CSLibrary.Constants.TagAccess.READ,
														m_rdr_opt_parms.TagReadAccPwd.password));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_READ_KILL_PWD:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.KILL_PWD,
														CSLibrary.Constants.TagAccess.READ,
														m_rdr_opt_parms.TagReadKillPwd.password));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_READ_TID:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.TID,
														CSLibrary.Constants.TagAccess.READ,
														m_rdr_opt_parms.TagReadTid.tid));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_READ_USER:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														Bank.USER,
														TagAccess.READ,
														m_rdr_opt_parms.TagReadUser.pData));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_WRITE_PC:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.PC,
														CSLibrary.Constants.TagAccess.WRITE,
														m_rdr_opt_parms.TagReadPC.pc));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_WRITE_EPC:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														Bank.EPC,
														CSLibrary.Constants.TagAccess.WRITE,
														m_rdr_opt_parms.TagReadEPC.epc));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_WRITE_ACC_PWD:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.ACC_PWD,
														CSLibrary.Constants.TagAccess.WRITE,
														m_rdr_opt_parms.TagReadAccPwd.password));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_WRITE_KILL_PWD:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														CSLibrary.Constants.Bank.KILL_PWD,
														CSLibrary.Constants.TagAccess.WRITE,
														m_rdr_opt_parms.TagReadKillPwd.password));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_WRITE_USER:
												{
													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														Bank.USER,
														CSLibrary.Constants.TagAccess.WRITE,
														m_rdr_opt_parms.TagReadUser.pData));
												}
												break;

											case CSLibrary.Constants.Operation.TAG_LOCK:
												{
													CSLibrary.Debug.WriteLine("Tag lock end {0}", currentCommandResponse);

													FireAccessCompletedEvent(
														new OnAccessCompletedEventArgs(
														(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
														Bank.UNKNOWN,
														TagAccess.LOCK,
														null));
												}
												break;
										}
									}

									FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
									break;

								case 0x0005:    /// inventory packet
									{
                                        InventoryDebug.InventoryPackerCountInc();

                                        byte[] packetData = _dataBuffer.DataOut(packetLen);

										R2000Packet_Inventory(packetData);
									}
									break;

								case 0x0006:    // Tag access Packet
									{
										byte[] packetData = _dataBuffer.DataOut(packetLen);

										if (R2000Packet_TagAccess(packetData))
											result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
									}
									break;

                                case 0x0007:    // Antenna-Cycle-End Packet
                                    {
                                        byte[] packetData = _dataBuffer.DataOut(packetLen);
                                    }
                                    break;

                                case 0x000A:    // INVENTORY_CYCLE_BEGIN
                                    {
                                        byte[] packetData = _dataBuffer.DataOut(packetLen);
                                    }
                                    break;

                                case 0x3007:    // RFID_PACKET_OEMCFG_READ
                                    {
                                        byte[] packetData = _dataBuffer.DataOut(packetLen);

                                        var m_OEMReadAdd = (UInt32)(packetData[8] | packetData[9] << 8 | packetData[10] << 16 | packetData[11] << 24);
                                        var m_OEMReadData = (UInt32)(packetData[12] | packetData[13] << 8 | packetData[14] << 16 | packetData[15] << 24);

                                        StoreOEMData(m_OEMReadAdd, m_OEMReadData);

                                        result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
                                    }
                                    break;
                            }
                        }
						break;
					}
				}

			return result;
		}

#else

		byte[] _RingBuffer = new byte [100];
        int _RingBufferDataSize = 0;

		/// <summary>
		/// Transfer BT API packet to R2000 packet
		/// </summary>
		/// <param name="recvData"></param>
		/// <param name="offset"></param>
		/// <param name="size"></param>
		public CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE DeviceRecvData(byte[] recvData1, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE currentCommandResponse)
        {
            Array.Copy(recvData1, 10, _RingBuffer, _RingBufferDataSize, recvData1[2] - 2);
            _RingBufferDataSize += recvData1[2] - 2;

			CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE result = HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.NOWAIT;

            byte[] recvData = _RingBuffer;
			int offset = 0;   // packet header

            while (offset < (_RingBufferDataSize - 7))
			{
				if (_dataBuffer.length == 0)
				{
					// first packet
					byte header = recvData[offset];

					switch (header)
					{
						case 0x40:  // Abort packet
							{
								result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA2;
								offset += 8;
							}
							break;

						case 0x00:
						case 0x70:  // register read packet
							{
								UInt16 add = (UInt16)(recvData[offset + 3] << 8 | recvData[offset + 2]);
								UInt32 data = (UInt16)(recvData[offset + 7] << 24 | recvData[offset + 6] << 16 | recvData[offset + 5] << 8 | recvData[offset + 4]);

								_registerData[add] = data;

								result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
							}

							offset += 8;
							break;

						default:
							{
								int pkt_type = BitConverter.ToUInt16(recvData, offset + 2) & 0x7fff;
								int packetLen = BitConverter.ToUInt16(recvData, offset + 4) * 4 + 8;

								switch (pkt_type)
								{
									default:
										offset = _RingBufferDataSize;
										break;

									case 0x0000:    // Command begin Packet
										offset += packetLen;
										break;

									case 0x0001:    // Command end Packet
										result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.COMMANDENDRESPONSE;
										offset += packetLen;

										// Check Tag access packet
										//                                        if ((currentCommandResponse | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0)
										{
											Operation RealCurrentOperation = (Operation)(_deviceHandler._currentCmdRemark);

											switch (RealCurrentOperation)
											{
												case CSLibrary.Constants.Operation.TAG_READ_PC:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.PC,
															CSLibrary.Constants.TagAccess.READ,
															m_rdr_opt_parms.TagReadPC.pc));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_READ_EPC:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.EPC,
															TagAccess.READ,
															m_rdr_opt_parms.TagReadEPC.epc));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_READ_ACC_PWD:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.ACC_PWD,
															CSLibrary.Constants.TagAccess.READ,
															m_rdr_opt_parms.TagReadAccPwd.password));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_READ_KILL_PWD:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.KILL_PWD,
															CSLibrary.Constants.TagAccess.READ,
															m_rdr_opt_parms.TagReadKillPwd.password));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_READ_TID:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.TID,
															CSLibrary.Constants.TagAccess.READ,
															m_rdr_opt_parms.TagReadTid.tid));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_READ_USER:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.USER,
															TagAccess.READ,
															m_rdr_opt_parms.TagReadUser.pData));
													}
													break;

                                                case CSLibrary.Constants.Operation.TAG_WRITE_PC:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.PC,
                                                            CSLibrary.Constants.TagAccess.WRITE,
															m_rdr_opt_parms.TagReadPC.pc));
													}
													break;

                                                case CSLibrary.Constants.Operation.TAG_WRITE_EPC:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.EPC,
															CSLibrary.Constants.TagAccess.WRITE,
															m_rdr_opt_parms.TagReadEPC.epc));
													}
													break;

                                                case CSLibrary.Constants.Operation.TAG_WRITE_ACC_PWD:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.ACC_PWD,
															CSLibrary.Constants.TagAccess.WRITE,
															m_rdr_opt_parms.TagReadAccPwd.password));
													}
													break;

                                                case CSLibrary.Constants.Operation.TAG_WRITE_KILL_PWD:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															CSLibrary.Constants.Bank.KILL_PWD,
															CSLibrary.Constants.TagAccess.WRITE,
															m_rdr_opt_parms.TagReadKillPwd.password));
													}
													break;

                                                case CSLibrary.Constants.Operation.TAG_WRITE_USER:
													{
														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.USER,
															CSLibrary.Constants.TagAccess.WRITE,
															m_rdr_opt_parms.TagReadUser.pData));
													}
													break;

												case CSLibrary.Constants.Operation.TAG_LOCK:
													{
														CSLibrary.Debug.PrintLine("Tag lock end {0}", currentCommandResponse);

														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.UNKNOWN,
															TagAccess.LOCK,
															null));
													}
													break;
											}
										}

										FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
										break;

									case 0x0005:    /// inventory packet
										R2000Packet_Inventory(recvData, offset);
										offset += packetLen;
										break;

									case 0x0006:    // Tag access Packet
										if (R2000Packet_TagAccess(recvData, offset))
											result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
										offset += packetLen;
										break;
								}
							}
							break;
					}
				}
			}

            if (_RingBufferDataSize == offset)
            {
                _RingBufferDataSize = 0;
            }
            else
            {
                _RingBufferDataSize -= offset;
                Array.Copy(_RingBuffer, offset, _RingBuffer, 0, _RingBufferDataSize);    
            }

			return result;
		}
#endif

#if oldcode
        /// <summary>
        /// Transfer BT API packet to R2000 packet
        /// </summary>
        /// <param name="recvData"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        public CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE DeviceRecvData_old(byte[] recvData, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE currentCommandResponse)
		{
            CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE result = HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.NOWAIT;

            int realProcessDataSize = recvData[2] + 8;     // 10 - 2
            int offset = 10;   // packet header

            while (offset < realProcessDataSize)
            {
                if (_dataBuffer.length == 0)
                {
                    // first packet
                    byte header = recvData[offset];

                    switch (header)
                    {
                        case 0x40:  // Abort packet
                            {
                                result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA2;
                                offset += 8;
                            }
                            break;

                        case 0x00:
                        case 0x70:  // register read packet
                            {
                                UInt16 add = (UInt16)(recvData[offset + 3] << 8 | recvData[offset + 2]);
                                UInt32 data = (UInt16)(recvData[offset + 7] << 24 | recvData[offset + 6] << 16 | recvData[offset + 5] << 8 | recvData[offset + 4]);

                                _registerData[add] = data;

                                result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
                            }

                            offset += 8;
                            break;

                        default:
                            {
                                int pkt_type = BitConverter.ToUInt16(recvData, offset + 2) & 0x7fff;
                                int packetLen = BitConverter.ToUInt16(recvData, offset + 4) * 4 + 8;

                                switch (pkt_type)
                                {
                                    default:
                                        offset = realProcessDataSize;
                                        break;

                                    case 0x0000:    // Command begin Packet
                                        offset += packetLen;
                                        break;

                                    case 0x0001:    // Command end Packet
                                        result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.COMMANDENDRESPONSE;
                                        offset += packetLen;

                                        // Check Tag access packet
                                        //                                        if ((currentCommandResponse | HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0)
                                        {
                                            Operation RealCurrentOperation = (Operation)(_deviceHandler._currentCmdRemark);

                                            switch (RealCurrentOperation)
                                            {
                                                case CSLibrary.Constants.Operation.TAG_READ_PC:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            CSLibrary.Constants.Bank.PC,
                                                            CSLibrary.Constants.TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadPC.pc));
                                                    }
                                                    break;

                                                case CSLibrary.Constants.Operation.TAG_READ_EPC:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            Bank.EPC,
                                                            TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadEPC.epc));
                                                    }
                                                    break;

                                                case CSLibrary.Constants.Operation.TAG_READ_ACC_PWD:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            CSLibrary.Constants.Bank.ACC_PWD,
                                                            CSLibrary.Constants.TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadAccPwd.password));
                                                    }
                                                    break;

                                                case CSLibrary.Constants.Operation.TAG_READ_KILL_PWD:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            CSLibrary.Constants.Bank.KILL_PWD,
                                                            CSLibrary.Constants.TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadKillPwd.password));
                                                    }
                                                    break;

                                                case CSLibrary.Constants.Operation.TAG_READ_TID:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            CSLibrary.Constants.Bank.TID,
                                                            CSLibrary.Constants.TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadTid.tid));
                                                    }
                                                    break;

                                                case CSLibrary.Constants.Operation.TAG_READ_USER:
                                                    {
                                                        FireAccessCompletedEvent(
                                                            new OnAccessCompletedEventArgs(
                                                            (((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
                                                            Bank.USER,
                                                            TagAccess.READ,
                                                            m_rdr_opt_parms.TagReadUser.pData));
                                                    }
                                                    break;

												case CSLibrary.Constants.Operation.TAG_LOCK:
													{
														CSLibrary.Debug.PrintLine("Tag lock end {0}", currentCommandResponse);

														FireAccessCompletedEvent(
															new OnAccessCompletedEventArgs(
															(((currentCommandResponse | result) & HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1) != 0),
															Bank.UNKNOWN,
															TagAccess.LOCK,
															null));
													}
													break;
											}
										}

                                        FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                                        break;

                                    case 0x0005:    /// inventory packet
                                        R2000Packet_Inventory(recvData, offset);
                                        offset += packetLen;
                                        break;

                                    case 0x0006:    // Tag access Packet
                                        if (R2000Packet_TagAccess(recvData, offset))
                                            result |= HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1;
                                        offset += packetLen;
                                        break;
                                }
                            }
                            break;
                    }
                }
            }

            return result;
        }
#endif

        ////////////////////////////////////////////////////////////////////////////////
        // Name:        Radio::WriteMacMaskRegisters
        // Description: Writes the MAC mask registers (select or post-singulation).
        ////////////////////////////////////////////////////////////////////////////////
        void WriteMacMaskRegisters(UInt16 registerAddress, UInt32 bitCount, byte[] pMask)
        {
			const int BITS_PER_BYTE = 8;
            const int BYTES_PER_REGISTER = 4;
            const int BITS_PER_REGISTER = BITS_PER_BYTE * BYTES_PER_REGISTER;
            int pcnt = 0;

            // Figure out how many bytes are in the mask
            UInt32 byteCount = (bitCount + 7) / 8;

            // Now write each MAC mask register
            while (byteCount > 0)
            {
                    UInt32 registerValue = 0;
                    int leftShift = 0;
                    UInt32 loopCount = (byteCount > BYTES_PER_REGISTER ? BYTES_PER_REGISTER : byteCount);

                    // Decrement the byte count by the number of bytes put into the register
                    byteCount -= loopCount;

                    // Build up the register value
                    for (int cnt = 0; cnt < loopCount; cnt++)
                    {
                        registerValue |= ((uint)pMask[pcnt++] << leftShift);
                        leftShift += BITS_PER_BYTE;
                    }

                    // If it is the last byte of the mask, then we are going to zero out
                    // any bits not in the mask
                    if (byteCount == 0 && (bitCount % BITS_PER_BYTE) != 0)
                    {
                        UInt32 mask = 0xFFFFFFFF;
                        mask <<= (int)(BITS_PER_REGISTER - (BITS_PER_BYTE - (bitCount % BITS_PER_BYTE)));
                        mask >>= (int)(BITS_PER_REGISTER - (leftShift - (bitCount % BITS_PER_BYTE)));
                        registerValue &= ~mask;
                    }

                // Write the register
                MacWriteRegister((MACREGISTER)(registerAddress++), registerValue);
           }
       } // Radio::WriteMacMaskRegisters

        /// <summary>
        /// Configures the tag-selection criteria for the ISO 18000-6C select 
        /// command.  The supplied tag-selection criteria will be used for any 
        /// tag-protocol operations (i.e., Inventory, etc.) in 
        /// which the application specifies that an ISO 18000-6C select 
        /// command should be issued prior to executing the tag-protocol 
        /// operation (i.e., the SelectFlags.SELECT flag is provided to 
        /// the appropriate RFID_18K6CTag* function).  The tag-selection 
        /// criteria will stay in effect until the next call to 
        /// SetSelectCriteria.  Tag-selection criteria may not 
        /// be changed while a radio module is executing a tag-protocol 
        /// operation. 
        /// </summary>
        /// <param name="critlist">
        /// SelectCriteria array, containing countCriteria entries, of selection 
        /// criterion structures that are to be applied sequentially, beginning with 
        /// pCriteria[0], to the tag population.  If this field is NULL, 
        /// countCriteria must be zero. 
        ///</param>
        /// <returns></returns>

        public Result SetSelectCriteria(SelectCriterion[] critlist)
        {
            uint index;
            uint registerValue;

            if (critlist == null || critlist.Length == 0)
                return Result.INVALID_PARAMETER;

            try
            {
                SelectCriteria SC = new SelectCriteria();
                SC.countCriteria = (uint)critlist.Length;
                SC.pCriteria = (SelectCriterion[])critlist.Clone();


                index = 0;
                {
                    SelectCriterion pCriterion = SC.pCriteria[index];
                    SelectMask pMask = pCriterion.mask;
                    SelectAction pAction = pCriterion.action;

                    // Instruct the MAC as to which select mask we want to work with
                    MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_SEL, index);

                    // Create the HST_TAGMSK_DESC_CFG register value and write it to the MAC
                    registerValue = (0x01 |
                        (((uint)(pAction.target) & 0x07) << 1) |
                        (((uint)(pAction.action) & 0x07) << 4) |
                        (pAction.enableTruncate != 0x00 ? (uint)(1 << 7) : 0));
                    MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_CFG, registerValue);

                    // Create the HST_TAGMSK_BANK register value and write it to the MAC
                    registerValue = (uint)pMask.bank;
                    MacWriteRegister(MACREGISTER.HST_TAGMSK_BANK, registerValue);

                    // Write the mask offset to the HST_TAGMSK_PTR register
                    MacWriteRegister(MACREGISTER.HST_TAGMSK_PTR, (uint)pMask.offset);

                    // Create the HST_TAGMSK_LEN register and write it to the MAC
                    registerValue = (uint)(pMask.count);
                    MacWriteRegister(MACREGISTER.HST_TAGMSK_LEN, registerValue);

                    // Now write the MAC's mask registers
                    WriteMacMaskRegisters((ushort)MACREGISTER.HST_TAGMSK_0_3, pMask.count, pMask.mask);
                    // Set up the selection criteria
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                //				CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
#endif
                return Result.SYSTEM_CATCH_EXCEPTION;
            }
            return m_Result;
        }

        /*        public Result SetSelectCriteria(SelectCriterion[] critlist)
                {
                    uint index;
                    uint registerValue;

                    if (critlist == null || critlist.Length == 0)
                        return Result.INVALID_PARAMETER;

                    try
                    {
                        SelectCriteria SC = new SelectCriteria();
                        SC.countCriteria = (uint)critlist.Length;
                        SC.pCriteria = (SelectCriterion[])critlist.Clone();

                        for (index = 0; index < SC.countCriteria; index++)
                        {
                            SelectCriterion pCriterion = SC.pCriteria[index];
                            SelectMask pMask = pCriterion.mask;
                            SelectAction pAction = pCriterion.action;

                            // Instruct the MAC as to which select mask we want to work with
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_SEL, index);

                            // Create the HST_TAGMSK_DESC_CFG register value and write it to the MAC
                            registerValue = (0x01 |
                                (((uint)(pAction.target) & 0x07) << 1) |
                                (((uint)(pAction.action) & 0x07) << 4) |
                                (pAction.enableTruncate != 0x00 ? (uint)(1 << 7) : 0));
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_CFG, registerValue);

                            // Create the HST_TAGMSK_BANK register value and write it to the MAC
                            registerValue = (uint)pMask.bank;
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_BANK, registerValue);

                            // Write the mask offset to the HST_TAGMSK_PTR register
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_PTR, (uint)pMask.offset);

                            // Create the HST_TAGMSK_LEN register and write it to the MAC
                            registerValue = (uint)(pMask.count);
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_LEN, registerValue);

                            // Now write the MAC's mask registers
                            WriteMacMaskRegisters((ushort)MACREGISTER.HST_TAGMSK_0_3, pMask.count, pMask.mask);
                            // Set up the selection criteria
                        }

                        //while (index < RFID_18K6C_MAX_SELECT_CRITERIA_CNT)
                        while (index < 8)
                        {
                            // Instruct the MAC as to which select mask we want to work with
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_SEL, index);

                            // Set the descriptor to disabled
                            MacWriteRegister(MACREGISTER.HST_TAGMSK_DESC_CFG, 0);

                            index++;
                        }
                    }
                    catch (System.Exception ex)
                    {
        #if DEBUG
        //				CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
        #endif
                        return Result.SYSTEM_CATCH_EXCEPTION;
                    }
                    return m_Result;
                }
        */

        /// <summary>
        /// Configures the post-singulation match criteria to be used by the 
        /// RFID radio module.  The supplied post-singulation match criteria 
        /// will be used for any tag-protocol operations (i.e., 
        /// Inventory, etc.) in which the application specifies 
        /// that a post-singulation match should be performed on the tags 
        /// that are singulated by the tag-protocol operation (i.e., the 
        /// SelectFlags.POST_MATCH flag is provided to the 
        /// appropriate RFID_18K6CTag* function).  The post-singulation 
        /// match criteria will stay in effect until the next call to 
        /// SetPostMatchCriteria.  Post-singulation match 
        /// criteria may not be changed while a radio module is executing a 
        /// tag-protocol operation. 
        /// </summary>
        /// <param name="postmatch"> An array that specifies the post-
        /// singulation match criteria that are to be 
        /// applied to the tag's Electronic Product Code 
        /// after it is singulated to determine if it is to 
        /// have the tag-protocol operation applied to it.  
        /// If the countCriteria field is zero, all post-
        /// singulation criteria will be disabled.  This 
        /// parameter must not be NULL. </param>
        /// <returns></returns>
        public Result SetPostMatchCriteria(SingulationCriterion[] postmatch)
		{
			UInt32 registerValue;

			try
			{
				if (postmatch.Length != 0)
				{
					// Set up the post-singulation match criteria
					//                    pCriterion = pParms->pCriteria;
					//                    const RFID_18K6C_SINGULATION_MASK* pMask = &pCriterion->mask;

					SingulationMask pMask = postmatch[0].mask;

					// Set up the HST_INV_EPC_MATCH_CFG register and write it to the MAC.
					// For now, we are going to assume that the singulation match should be
					// enabled (if the application so desires, we can turn it off when we
					// actually do the tag-protocol operation).
					registerValue =
                        (uint)(postmatch[0].match != 0 ? 0 : 2) |
						(uint)(postmatch[0].mask.count << 2) |
						(uint)(postmatch[0].mask.offset << 11);

                    MacWriteRegister(MACREGISTER.HST_INV_EPC_MATCH_SEL, 0X00);
					MacWriteRegister(MACREGISTER.HST_INV_EPC_MATCH_CFG, registerValue);

					// Now write the MAC's mask registers
					WriteMacMaskRegisters((UInt16)MACREGISTER.HST_INV_EPCDAT_0_3, pMask.count, pMask.mask);
				}
				else // must be calling to disable criteria
				{
					MacWriteRegister(MACREGISTER.HST_INV_EPC_MATCH_CFG, 0);
				}
			}
			catch (System.Exception ex)
			{
#if DEBUG
//				CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.SetSelectCriteria()", ex);
#endif
				return Result.SYSTEM_CATCH_EXCEPTION;
			}
			return m_Result;
		}

#region ====================== Set Tag Group ======================
		/// <summary>
		/// Once the tag population has been partitioned into disjoint groups, a subsequent 
		/// tag-protocol operation (i.e., an inventory operation or access command) is then 
		/// applied to one of the tag groups. 
		/// </summary>
		/// <param name="gpSelect">Specifies the state of the selected (SL) flag for tags that will have 
		/// the operation applied to them. </param>
		/// <param name="gpSession">Specifies which inventory session flag (i.e., S0, S1, S2, or S3) 
		/// will be matched against the inventory state specified by target. </param>
		/// <param name="gpSessionTarget">Specifies the state of the inventory session flag (i.e., A or B),
		/// specified by session, for tags that will have the operation 
		/// applied to them. </param>
		public Result SetTagGroup(Selected gpSelect, Session gpSession, SessionTarget gpSessionTarget)
		{
			UInt32 value = 0;

			//DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetTagGroup(Selected gpSelect, Session gpSession, SessionTarget gpSessionTarget)");

			MacReadRegister(MACREGISTER.HST_QUERY_CFG, ref value);

			value &= ~0x01f0U;
			value |= ((uint)gpSessionTarget << 4) | ((uint)gpSession << 5) | ((uint)gpSelect << 7);

			MacWriteRegister(MACREGISTER.HST_QUERY_CFG, value);

			return Result.OK;

			/*            return (m_Result = MacWriteRegister(MACREGISTER.HST_QUERY_CFG,
							(uint)gpSessionTarget << 4 |
							(uint)gpSession << 5 |
							(uint)gpSelect << 7));*/
		}

		/// <summary>
		/// Once the tag population has been partitioned into disjoint groups, a subsequent 
		/// tag-protocol operation (i.e., an inventory operation or access command) is then 
		/// applied to one of the tag groups. 
		/// </summary>
		/// <param name="tagGroup"><see cref="TagGroup"/></param>
		/// <returns></returns>
		public Result SetTagGroup(TagGroup tagGroup)
		{
			UInt32 value = 0;

			//DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.SetTagGroup(TagGroup tagGroup)");

			MacReadRegister(MACREGISTER.HST_QUERY_CFG, ref value);

			value &= ~0x01f0U;
			value |= ((uint)tagGroup.target << 4) | ((uint)tagGroup.session << 5) | ((uint)tagGroup.selected << 7);

			MacWriteRegister(MACREGISTER.HST_QUERY_CFG, value);

			return Result.OK;

			/*            return (m_Result = MacWriteRegister(MACREGISTER.HST_QUERY_CFG,
							(uint)tagGroup.target << 4 |
							(uint)tagGroup.session << 5 |
							(uint)tagGroup.selected << 7));
			*/
		}
		/// <summary>
		/// Get Tag Group
		/// </summary>
		/// <param name="tagGroup"></param>
		/// <returns></returns>
		public Result GetTagGroup(TagGroup tagGroup)
		{
			//UInt16 HST_QUERY_CFG = 0x0900;

			UInt32 registerValue = 0;

			//DEBUG_WriteLine(DEBUGLEVEL.API, "HighLevelInterface.GetTagGroup(TagGroup tagGroup)");

			MacReadRegister(MACREGISTER.HST_QUERY_CFG, ref registerValue);

			tagGroup.selected = (Selected)((registerValue >> 7) & 0x03);
			tagGroup.session = (Session)((registerValue >> 5) & 0x03);
			tagGroup.target = (SessionTarget)((registerValue >> 4) & 0x01);

			return (m_Result = Result.OK);
		}
#endregion

#if oldcode
        public void DeviceRecvData (byte [] recvData, int offset, int size)
        {
            _dataBuffer.DataIn(recvData, offset, size);

            // try to analysis first packet
            if (_dataBuffer.length >= 8)
            {
                byte[] header = _dataBuffer.DataPreOut();

                switch (header[0])
                {
                    case 0x70:  // register read packet
                        {
                            byte[] registerPacket = _dataBuffer.DataOut(8);

                            UInt16 add = (UInt16)(registerPacket[3] << 8 | registerPacket[2]);
                            UInt32 data = (UInt16)(registerPacket[7] << 24 | registerPacket[6] << 16 | registerPacket[5] << 8 | registerPacket[4]);

                            _registerData[add] = data;
                        }
                        break;

                    case 0x00:
                        {
                            byte[] registerPacket = _dataBuffer.DataOut(8);

                            if (registerPacket[1] == 0x70)
                            {
                                //cycle end
                            }
                        }
                        break;

                    case 0x01:  //
                        {
                            byte[] CommandPacket = _dataBuffer.DataOut(8);
                        }
                        break;

                    case 0x02:  //
                        {
                            byte[] CommandPacket = _dataBuffer.DataOut(8);

                            if (CommandPacket[3] == 0x80)   // Command packet
                            {
                                _dataBuffer.DataOut(8);
                            }
                            else
                            {
                                break;
                            }
                            switch (CommandPacket[2])
                            {
                                case 0x00:      // Command Begin
                                    break;

                                case 0x01:      // Command End
                                    break;
                                       
                                default:        // Error Code
                                    break;
                            }
                        }
                        break;

                    case 0x03:  // inventory 
                        {
                            byte[] tagPacketHeader = _dataBuffer.DataOut(8);

                            var pkt_ver = tagPacketHeader[0];
                            var flags = tagPacketHeader[1];
                            var pkt_type = (UInt16)(tagPacketHeader[2] | (tagPacketHeader[3] << 8));
                            var pkt_len = (UInt16)(tagPacketHeader[4] | (tagPacketHeader[5] << 8));
                            var extdatalen = (pkt_len) * 4 - ((flags >> 6) & 3);

                            switch (pkt_type)
                            {
                                case 0x8005:    /// inventory
                                    {
                                        int pktByteLen = pkt_len * 4;

                                        if (_dataBuffer.length < pktByteLen)
                                        {
                                            _dataBuffer.Clear();
                                            return;
                                        }

                                        byte[] tagPacketBody = _dataBuffer.DataOut(pktByteLen);

                                        if (OnAsyncCallback != null)
                                        {
                                            CSLibrary.Events.TagCallbackInfo info = new CSLibrary.Events.TagCallbackInfo();
                                            CSLibrary.Events.CallbackType type = CSLibrary.Events.CallbackType.TAG_RANGING;

                                            info.ms_ctr = (UInt32)(tagPacketBody[0] | tagPacketBody[1] << 8 | tagPacketBody[2] << 16 | tagPacketBody[3] << 24);
                                            info.rssi = (Single)(tagPacketBody[5] * 0.8);
                                            info.freqChannel = tagPacketBody[10];
                                            info.antennaPort = (UInt16)(tagPacketBody[10] | tagPacketBody[11] << 8);
                                            info.pc = (UInt16)(tagPacketBody[12] << 8 | tagPacketBody[13]);
                                            info.epcstrlen = (UInt16)((((pkt_len - 3) * 4) - ((flags >> 6) & 3) - 4));
                                            info.crc16 = (UInt16)(tagPacketBody[14 + info.epcstrlen] << 8 | tagPacketBody[15 + info.epcstrlen]);

                                            byte[] byteEpc = new byte[info.epcstrlen];
                                            Array.Copy(tagPacketBody, 14, byteEpc, 0, (int)info.epcstrlen);

                                            info.epc = (ushort[])CSLibrary.Tools.Hex.ToUshorts(byteEpc).Clone();

                                            CSLibrary.Events.OnAsyncCallbackEventArgs callBackData = new Events.OnAsyncCallbackEventArgs(info, type);
                                            OnAsyncCallback(_deviceHandler, callBackData);
                                        }
                                    }
                                    break;

                                default:        // unknown packet
                                    break;
                            }


                        }
                        break;

                    default:    // skip invalid packet
                        _dataBuffer.Clear();
                        break;
                }
            }


            /*
                        switch (_readerStatus)
                        {
                            case RFIDREADERCMDSTATUS.IDLE:
                                break;

                            case RFIDREADERCMDSTATUS.GETREGISTER:
                                if (_dataBuffer.length >= 8)
                                {
                                    byte [] header = _dataBuffer.DataPreOut(4);
                                    if (Array.Equals(header, new byte []{0x01, 0x02, 0x03, 0x04 }))
                                    {
                                        byte [] getRegiterPacket = _dataBuffer.DataOut(8);
                                    }
                                }
                                _readerStatus = RFIDREADERCMDSTATUS.IDLE;
                                break;

                            case RFIDREADERCMDSTATUS.EXECCMD:  // Receive command begin packet
                                if (_dataBuffer.length >= 16)
                                {

                                }
                                break;

                            case RFIDREADERCMDSTATUS.INVENTORY:  // Receive inventory packet
                                if (_dataBuffer.length >= 16)
                                {

                                }
                                break;

                            case RFIDREADERCMDSTATUS.ABORT: // Receive bbort response
                                if (_dataBuffer.length >= 8)
                                {
                                    byte[] packetData = _dataBuffer.DataPreOut(8);
                                    if (Array.Equals(packetData, new byte[] { 0x43, 0x02, 0x03, 0x04 }))
                                    {
                                        _dataBuffer.Skip (8);
                                    }
                                }
                                _readerStatus = RFIDREADERCMDSTATUS.IDLE;
                                break;
                        }

                */

        }
#endif

		// public RFID function
		internal void PowerOn()
		{
			CSLibrary.Debug.WriteLine("DateTime {0}", DateTime.Now);

			_deviceHandler.SendAsync(0, 0, DOWNLINKCMD.RFIDPOWERON);
		}

		internal void PowerOff()
		{
			CSLibrary.Debug.WriteLine("DateTime {0}", DateTime.Now);

			_deviceHandler.SendAsync(0, 0, DOWNLINKCMD.RFIDPOWEROFF);
		}

        internal UInt32 _INVENTORYDELAYTIME = (7 << 20);
        internal UInt32 _InventoryCycleDelay = 0x00;

        public bool SetInventoryTimeDelay(UInt32 ms)
		{
			if (ms > 0x3f)
				return false;

            _INVENTORYDELAYTIME = (ms << 20);

        /*
            UInt32 value = 0;

			MacReadRegister(MACREGISTER.HST_INV_CFG, ref value);

			value &= ~(0x03f00000U);
			value |= (ms << 20);

			MacWriteRegister(MACREGISTER.HST_INV_CFG, value);
        */
			return true;
		}

        public bool SetInventoryCycleDelay(UInt32 ms)
        {
            _InventoryCycleDelay = ms;

            /*
                UInt32 value = 0;

                MacReadRegister(MACREGISTER.HST_INV_CFG, ref value);

                value &= ~(0x03f00000U);
                value |= (ms << 20);

                MacWriteRegister(MACREGISTER.HST_INV_CFG, value);
            */
            return true;
        }


        #region Public Functions

        internal RFIDReader(HighLevelInterface deviceHandler)
		{
			_deviceHandler = deviceHandler;
		}

		~RFIDReader()
		{
		}


		/*		readonly UInt16[,] _mainRegister = new UInt16[0x0c, 2]  {
																{ 0x000, 0x002 },
																{ 0x100, 0x100 },
																{ 0x200, 0x200 },
																{ 0x300, 0x300 },
																{ 0x400, 0x400 },
																{ 0x500, 0x500 },
																{ 0x600, 0x600 },
																{ 0x700, 0x700 },
																{ 0x800, 0x800 },
																{ 0x900, 0x902 },
																{ 0xa00, 0xa00 },
																{ 0xb00, 0xb02 }};
		*/

		internal void Connect()
		{
            MacRegisterInitialize();
            ReadReaderOEMData();
            //MacRegisterInitialize();



            //			ReadReaderRegister((UInt16)MACREGISTER.HST_ANT_CYCLES);

            //			MacWriteRegister(MACREGISTER.HST_ANT_DESC_SEL, 0);  // Set Antenna 0 

            /*
                        ReadReaderRegister((UInt16)MACREGISTER.HST_ANT_DESC_RFPOWER);   // Get Antenna 0 Power Level
                        ReadReaderRegister((UInt16)MACREGISTER.HST_ANT_DESC_DWELL);

                        ReadReaderRegister((UInt16)MACREGISTER.HST_QUERY_CFG);
                        ReadReaderRegister((UInt16)MACREGISTER.HST_INV_CFG);
                        ReadReaderRegister((UInt16)MACREGISTER.HST_INV_EPC_MATCH_CFG);

                        ReadReaderRegister((UInt16)MACREGISTER.HST_TAGACC_DESC_CFG);

                        ReadReaderRegister(0x005); // reader mac error register
            */

        }

        internal void Reconnect()
		{
		}

        public UInt32 GetFirmwareVersion()
        {
            UInt32 value = 0, value1;

            MacReadRegister(MACREGISTER.MAC_VER, ref value);

            value1 = (0xfff & value) | ((0xfff & (value >> 12)) << 8) | ((0xff & (value >> 24)) << 16);

            return value1;
        }

		/// <summary>
		/// Retrieves the operation mode for the RFID radio module.  The 
		/// operation mode cannot be retrieved while a radio module is 
		/// executing a tag-protocol operation. 
		/// </summary>
		/// <param name="cycles">The number of antenna cycles to be completed for command execution.
		/// <para>0x0001 = once cycle through</para>
		/// <para>0xFFFF = cycle forever until a CANCEL is received.</para></param>
		/// <param name="mode">Antenna Sequence mode.</param>
		/// <param name="sequenceSize">Sequence size. Maximum value is 48</param>
		/// <returns></returns>
		public Result GetOperationMode(ref ushort cycles, ref AntennaSequenceMode mode, ref uint sequenceSize)
		{
			uint value = 0;

			MacReadRegister(MACREGISTER.HST_ANT_CYCLES /*0x700*/, ref value);

			cycles = (ushort)(0xffff & value);
			mode = (AntennaSequenceMode)((value >> 16) & 0x3);
			sequenceSize = (value >> 18) & 0x3F;

			return Result.OK;
		}

		/// <summary>
		/// Retrieves the operation mode for the RFID radio module.  The 
		/// operation mode cannot be retrieved while a radio module is 
		/// executing a tag-protocol operation. 
		/// </summary>
		/// <param name="mode"> return will receive the current operation mode.</param>
		/// <returns></returns>
		public void GetOperationMode(ref RadioOperationMode mode)
		{
			UInt32 value = 0;

			MacReadRegister(MACREGISTER.HST_ANT_CYCLES /*0x700 HST_ANT_CYCLES*/, ref value);

			if ((value & 0xffff) == 0xffff)
				mode = RadioOperationMode.CONTINUOUS;
			else
				mode = RadioOperationMode.NONCONTINUOUS;
		}

		/// <summary>
		/// Sets the operation mode of RFID radio module.  By default, when 
		/// an application opens a radio, the RFID Reader Library sets the 
		/// reporting mode to non-continuous.  An RFID radio module's 
		/// operation mode will remain in effect until it is explicitly changed 
		/// via RFID_RadioSetOperationMode, or the radio is closed and re-
		/// opened (at which point it will be set to non-continuous mode).  
		/// The operation mode may not be changed while a radio module is 
		/// executing a tag-protocol operation. 
		/// </summary>
		/// <param name="mode">The operation mode for the radio module.</param>
		/// <returns></returns>
		public Result SetOperationMode(RadioOperationMode mode)
		{
			AntennaSequenceMode smode = AntennaSequenceMode.NORMAL;
			uint sequenceSize = 0;

			if (RadioOperationMode.UNKNOWN == mode)
				return Result.INVALID_PARAMETER;

			SetOperationMode((ushort)(mode == RadioOperationMode.CONTINUOUS ? 0xFFFF : 1), smode, sequenceSize);

			return Result.OK;
		}

		/// <summary>
		/// Sets the operation mode of RFID radio module.  By default, when 
		/// an application opens a radio, the RFID Reader Library sets the 
		/// reporting mode to non-continuous.  An RFID radio module's 
		/// operation mode will remain in effect until it is explicitly changed 
		/// via RFID_RadioSetOperationMode, or the radio is closed and re-
		/// opened (at which point it will be set to non-continuous mode).  
		/// The operation mode may not be changed while a radio module is 
		/// executing a tag-protocol operation. 
		/// </summary>
		/// <param name="cycles">The number of antenna cycles to be completed for command execution.
		/// <para>0x0001 = once cycle through</para>
		/// <para>0xFFFF = cycle forever until a CANCEL is received.</para></param>
		/// <returns></returns>
		public Result SetOperationMode(UInt16 cycles)
		{
			bool result = false;
			uint value = 0, value1 = 0;

			if (!MacReadRegister(MACREGISTER.HST_ANT_CYCLES/*0x700*/, ref value))
				return Result.FAILURE;

			value1 = cycles;
			if (((value >> 24) & 0x01) != 0)
				value1 |= (0x01 << 24);

			MacWriteRegister(MACREGISTER.HST_ANT_CYCLES/*0x700*/, value1);

			return Constants.Result.OK;
		}



		/// <summary>
		/// Sets the operation mode of RFID radio module.  By default, when 
		/// an application opens a radio, the RFID Reader Library sets the 
		/// reporting mode to non-continuous.  An RFID radio module's 
		/// operation mode will remain in effect until it is explicitly changed 
		/// via RFID_RadioSetOperationMode, or the radio is closed and re-
		/// opened (at which point it will be set to non-continuous mode).  
		/// The operation mode may not be changed while a radio module is 
		/// executing a tag-protocol operation. 
		/// </summary>
		/// <param name="cycles">The number of antenna cycles to be completed for command execution.
		/// <para>0x0001 = once cycle through</para>
		/// <para>0xFFFF = cycle forever until a CANCEL is received.</para></param>
		/// <param name="mode">Antenna Sequence mode.</param>
		/// <param name="sequenceSize">Sequence size. Maximum value is 48</param>
		/// <returns></returns>
		public Result SetOperationMode(ushort cycles, Events.AntennaSequenceMode mode = AntennaSequenceMode.NORMAL, uint sequenceSize = 0)
		{
			uint value = 0;

			if (sequenceSize > 48)
				return Result.INVALID_PARAMETER;

			value = (cycles | ((uint)mode & 0x3) << 16 | (sequenceSize & 0x3F) << 18);
			MacWriteRegister(MACREGISTER.HST_ANT_CYCLES, value);
            return Result.OK;
		}


		/// <summary>
		/// This is used to set inventory duration
		/// </summary>
		/// <param name="duration"></param>
		/// <returns></returns>
		public Result SetInventoryDuration(uint duration, uint antennaPort = 0)
		{
			MacWriteRegister(MACREGISTER.HST_ANT_DESC_SEL, antennaPort);
			MacWriteRegister(MACREGISTER.HST_ANT_DESC_DWELL, duration);

			return Result.OK;
		}
#endregion

	}
}


class MACREGISTER
{
}