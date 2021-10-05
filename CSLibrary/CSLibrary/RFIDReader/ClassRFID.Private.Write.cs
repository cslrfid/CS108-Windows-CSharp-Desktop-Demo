using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public partial class RFIDReader
    {
        const int MAX_WR_CNT = 0x8;

        void Setup18K6CWriteRegisters(CSLibrary.Constants.MemoryBank WriteBank, uint WriteOffset, uint WriteSize, UInt16[] WriteBuf, uint BufOffset)
        {
            int offset;
            int pcnt = 0;

            // Set up the tag bank register (tells where to write the data)
            MacWriteRegister(MACREGISTER.HST_TAGACC_BANK, (uint)WriteBank);

            // Set the offset
            //MacWriteRegister(MACREGISTER.HST_TAGACC_PTR, WriteOffset);
            MacWriteRegister(MACREGISTER.HST_TAGACC_PTR, 0);

            // Set up the access count register (i.e., number of words to write)
            MacWriteRegister(MACREGISTER.HST_TAGACC_CNT, WriteSize);

            // Set up the HST_TAGWRDAT_N registers.  Fill up a bank at a time.
            for (UInt32 registerBank = 0; WriteSize > 0; registerBank++)
            {
                uint value = 0;

                // Indicate which bank of tag write registers we are going to fill
                MacWriteRegister(MACREGISTER.HST_TAGWRDAT_SEL, registerBank);

                /*
				MacReadRegister(MACREGISTER.MAC_ERROR, ref value);

				if (value == HOSTIF_ERR_SELECTORBNDS)
				{
					MacClearError();
					return;
				}
				*/

                // Write the values to the bank until either the bank is full or we run out of data
                UInt16 registerAddress = (UInt16)MACREGISTER.HST_TAGWRDAT_0;
                offset = 0;

                while ((WriteSize > 0) && (offset < 16 /*RFID_NUM_TAGWRDAT_REGS_PER_BANK*/))
                {
                    // Set up the register and then write it to the MAC
                    UInt32 registerValue = (uint)(WriteBuf[BufOffset + pcnt] | ((WriteOffset + pcnt) << 16));

                    MacWriteRegister((MACREGISTER)(registerAddress), registerValue);

                    pcnt++;
                    registerAddress++;
                    offset++;
                    WriteSize--;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bank"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="data"></param>
        /// <param name="password"></param>
        /// <param name="retry"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private CSLibrary.Constants.Result CUST_18K6CTagWrite(
            CSLibrary.Constants.MemoryBank bank,
            UInt32 offset,
            UInt32 count,
            UInt16[] data,
            UInt32 password,
            //UInt32 retry,
            //UInt32 writeretry,
            CSLibrary.Constants.SelectFlags flags
        )
        {
            int index;
            uint wrCycle = (uint)(count / MAX_WR_CNT);
            uint wrReminder = (uint)(count % MAX_WR_CNT);
            CSLibrary.Constants.Result status;
            UInt32 i;

            if (wrCycle > 0)
                return CSLibrary.Constants.Result.DEVICE_NOT_SUPPORT;

            //MacWriteRegister(MACREGISTER.HST_TAGACC_DESC_CFG, 0x1ff);
            MacWriteRegister(MACREGISTER.HST_TAGACC_ACCPWD, password);
            Start18K6CRequest(1, flags);

            //ReadReaderRegister((UInt16)MACREGISTER.HST_TAGACC_DESC_CFG);
            MacWriteRegister(MACREGISTER.HST_TAGACC_DESC_CFG  /*0xA01*/, (31 << 1) | 0x01); // Enable write verify and set retry count
                                                                                                    //MacWriteRegister(MACREGISTER.HST_TAGACC_DESC_CFG  /*0xA01*/, 0x1ff); // Enable write verify and set retry count

            /*			for (index = 0; index < wrCycle; index++)
						{
							Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), MAX_WR_CNT, data, (uint)(index * MAX_WR_CNT));

							for (i = retry; i > 0; i--)
							{
								// Issue the write command to the MAC
								status = COMM_HostCommand(HST_CMD.WRITE);

								if (status != Result.OK)
									return status;

								//MacClearError();

								if (m_TagAccessStatus == 2)
									break;

								System.Threading.Thread.Sleep(100);
							}

							if (i == 0)
								return Result.MAX_RETRY_EXIT;
						}*/
            index = 0;
            if (wrReminder > 0)
            {
                Setup18K6CWriteRegisters(bank, (uint)(offset + index * MAX_WR_CNT), wrReminder, data, (uint)(index * MAX_WR_CNT));

                //for (i = retry; i > 0; i--)
                {
                    // Issue the write command to the MAC
                    //status = COMM_HostCommand(HST_CMD.WRITE);
                    _deviceHandler.SendAsync(0, 0, DOWNLINKCMD.RFIDCMD, PacketData(0xf000, (UInt32)HST_CMD.WRITE), HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_COMMANDENDRESPONSE, (UInt32)CurrentOperation);

                    //if (status != Result.OK)
                    //return status;

                    //MacClearError();

                    //if (m_TagAccessStatus == 2)
                    //break;

                    //System.Threading.Thread.Sleep(100);
                }

                //if (i == 0)
                //return Result.MAX_RETRY_EXIT;
            }

            return CSLibrary.Constants.Result.OK;
        }

        private void TagWritePCThreadProc()
        {
            UInt16[] readData = new UInt16[1];
            UInt16[] writeData = new UInt16[1];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                //                m_Result = TagWritePC(m_rdr_opt_parms.TagWritePC);

                writeData[0] = m_rdr_opt_parms.TagWritePC.pc;
                                m_Result = CUST_18K6CTagWrite(
                                    CSLibrary.Constants.MemoryBank.EPC,
                                    PC_START_OFFSET,
                                    ONE_WORD_LEN,
                                    writeData,
                                    m_rdr_opt_parms.TagWritePC.accessPassword,
                                    //m_rdr_opt_parms.TagWritePC.retryCount,
                                    //m_rdr_opt_parms.TagWritePC.writeRetryCount,
                                    CSLibrary.Constants.SelectFlags.SELECT);
                }
                catch (System.Exception ex)
                {
    #if DEBUG
    //                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWritePCThreadProc()", ex);
    #endif
                }
                finally
                {
/*                            FireAccessCompletedEvent(
                        new OnAccessCompletedEventArgs(
                        m_Result == Result.OK,
                        Bank.PC,
                        TagAccess.WRITE,
                        new S_PC(m_rdr_opt_parms.TagWritePC.pc)));

                    FireStateChangedEvent(RFState.IDLE);
*/              }
        }

        private void TagWriteEPCThreadProc()
        {
            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                UInt16[] readData = new UInt16[m_rdr_opt_parms.TagWriteEPC.count];
                UInt16[] writeData = m_rdr_opt_parms.TagWriteEPC.epc.ToUshorts();
                UInt16[] readCmp = new UInt16[MAX_WR_CNT];
                bool status;

                m_Result = CSLibrary.Constants.Result.OK;

                m_Result = CUST_18K6CTagWrite(
                    CSLibrary.Constants.MemoryBank.EPC,
                    (uint)(EPC_START_OFFSET + m_rdr_opt_parms.TagWriteEPC.offset),
                    m_rdr_opt_parms.TagWriteEPC.count,
                    writeData,
                    m_rdr_opt_parms.TagWriteEPC.accessPassword,
                    //m_rdr_opt_parms.TagWriteEPC.retryCount,
                    //m_rdr_opt_parms.TagWriteEPC.writeRetryCount,
                    CSLibrary.Constants.SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteEPCThreadProc()", ex);
#endif
            }
            finally
            {
/*                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.EPC,
                    TagAccess.WRITE,
                    m_rdr_opt_parms.TagWriteEPC.epc));

                FireStateChangedEvent(RFState.IDLE);
*/            }
        }

        private void TagWriteAccPwdThreadProc()
        {
            UInt16[] writeData = new UInt16[2];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                //                m_Result = TagWriteAccPwd(m_rdr_opt_parms.TagWriteAccPwd);
                writeData[0] = (ushort)(m_rdr_opt_parms.TagWriteAccPwd.password >> 16);
                writeData[1] = (ushort)m_rdr_opt_parms.TagWriteAccPwd.password;

                m_Result = CUST_18K6CTagWrite(
                    CSLibrary.Constants.MemoryBank.RESERVED,
                    ACC_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    writeData,
                    m_rdr_opt_parms.TagWriteAccPwd.accessPassword,
                    //m_rdr_opt_parms.TagWriteAccPwd.retryCount,
                    //m_rdr_opt_parms.TagWriteAccPwd.writeRetryCount,
                    CSLibrary.Constants.SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteAccPwdThreadProc()", ex);
#endif
            }
            finally
            {
                /*
                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.ACC_PWD,
                    TagAccess.WRITE,
                    new S_PWD(m_rdr_opt_parms.TagWriteAccPwd.password)));

                FireStateChangedEvent(RFState.IDLE);*/
            }
        }

        private void TagWriteKillPwdThreadProc()
        {
            UInt16[] writeData = new UInt16[2];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                //m_Result = TagWriteKillPwd(m_rdr_opt_parms.TagWriteKillPwd);

                writeData[0] = (UInt16)(m_rdr_opt_parms.TagWriteKillPwd.password >> 16);
                writeData[1] = (UInt16)(m_rdr_opt_parms.TagWriteKillPwd.password);

                m_Result = CUST_18K6CTagWrite(
                    CSLibrary.Constants.MemoryBank.RESERVED,
                    KILL_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    writeData,
                    m_rdr_opt_parms.TagWriteKillPwd.accessPassword,
                    //m_rdr_opt_parms.TagWriteKillPwd.retryCount,
                    //m_rdr_opt_parms.TagWriteKillPwd.writeRetryCount,
                    CSLibrary.Constants.SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteKillPwdThreadProc()", ex);
#endif
            }
            finally
            {
/*                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.KILL_PWD,
                    TagAccess.WRITE,
                    new S_PWD(m_rdr_opt_parms.TagWriteKillPwd.password)));

                FireStateChangedEvent(RFState.IDLE);
*/            }
        }

        private void TagWriteUsrMemThreadProc()
        {
            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                UInt16[] readData = new UInt16[m_rdr_opt_parms.TagWriteUser.count];
                UInt16[] writeData = m_rdr_opt_parms.TagWriteUser.pData;
                UInt16[] readCmp = new UInt16[MAX_WR_CNT];
                bool status;

                m_Result = CSLibrary.Constants.Result.OK;

                m_Result = CUST_18K6CTagWrite(
                    CSLibrary.Constants.MemoryBank.USER,
                    (uint)(m_rdr_opt_parms.TagWriteUser.offset),
                    m_rdr_opt_parms.TagWriteUser.count,
                    writeData,
                    m_rdr_opt_parms.TagWriteUser.accessPassword,
                    //m_rdr_opt_parms.TagWriteUser.retryCount,
                    //m_rdr_opt_parms.TagWriteUser.writeRetryCount,
                    CSLibrary.Constants.SelectFlags.SELECT);
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagWriteUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
/*                FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == Result.OK,
                    Bank.USER,
                    TagAccess.WRITE,
                    new S_DATA(m_rdr_opt_parms.TagWriteUser.pData)));

                FireStateChangedEvent(RFState.IDLE);
*/            }
        }
    }
}
