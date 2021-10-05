using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public partial class RFIDReader
    {
        void Setup18K6CReadRegisters(UInt32 bank, UInt32 offset, UInt32 count)
        {
            // Set up the access bank register
            MacWriteRegister(MACREGISTER.HST_TAGACC_BANK, bank);

            // Set up the access pointer register (tells the offset)
            MacWriteRegister(MACREGISTER.HST_TAGACC_PTR, offset);

            // Set up the access count register (i.e., number values to read)
            MacWriteRegister(MACREGISTER.HST_TAGACC_CNT, count);
        }

        public int Start18K6CRead(uint bank, uint offset, uint count, UInt16[] data, uint accessPassword, uint retry, CSLibrary.Constants.SelectFlags flags)
        {
            // Perform the common 18K6C tag operation setup
            Start18K6CRequest(retry, flags);

            Setup18K6CReadRegisters(bank, offset, count);

            // Set up the access password register
            MacWriteRegister(MACREGISTER.HST_TAGACC_ACCPWD, accessPassword);

            // Issue the read command
            _deviceHandler.SendAsync(0, 0, DOWNLINKCMD.RFIDCMD, PacketData(0xf000, (UInt32)HST_CMD.READ), HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_COMMANDENDRESPONSE, (UInt32)CurrentOperation);

            return 0;
        } //  Start18K6CRead

        bool CUST_18K6CTagRead(CSLibrary.Constants.MemoryBank bank, int offset, int count, UInt16[] data, UInt32 password, /*UInt32 retry, */CSLibrary.Constants.SelectFlags flags)
        {
            const int MAX_RD_CNT = 0x20;

            int rdCycle = count / MAX_RD_CNT;
            int rdReminder = count % MAX_RD_CNT;
            int index = 0, i;

            //if (retry > 7)
            //    retry = 7;

            if (rdCycle > 0)
                return false;       // too many data

            /*
            for (index = 0; index < rdCycle; index++)
            {
                for (i = 0; i < retry; i++)
                    if (Start18K6CRead(bank, (uint)(offset + index * MAX_RD_CNT), MAX_RD_CNT, tagreadbuf, password, 1, flags) == true)
                    {
                        if (m_TagAccessStatus == 2)
                        {
                            Array.Copy(tagreadbuf, 0, data, index * MAX_RD_CNT, MAX_RD_CNT);
                            break;
                        }
                    }
                if (i == retry)
                    return false;
            }
            */

            if (rdReminder > 0)
            {
                Start18K6CRead((uint)bank, (uint)(offset + index * MAX_RD_CNT), (uint)rdReminder, data, password, 1, flags);
                /*
                    if (Start18K6CRead(bank, (uint)(offset + index * MAX_RD_CNT), (uint)rdReminder, tagreadbuf, password, 1, flags) == true)
                {
                    if (m_TagAccessStatus == 2)
                    {
                        Array.Copy(tagreadbuf, 0, data, index * MAX_RD_CNT, rdReminder);
                        break;
                    }
                }*/
            }
            return true;
        }

        private void TagReadPCThreadProc()
        {
            ushort[] readbuf = new ushort[1];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                CurrentOperationResult = CSLibrary.Constants.Result.NO_TAG_FOUND;

                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.BANK1,
                    PC_START_OFFSET,
                    ONE_WORD_LEN,
                    readbuf,
                    m_rdr_opt_parms.TagReadPC.accessPassword,
//                    m_rdr_opt_parms.TagReadPC.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadPC.m_pc = readbuf[0];
                else
                    m_Result = CSLibrary.Constants.Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadPCThreadProc()", ex);
#endif
            }
            finally
            {
                /*                FireAccessCompletedEvent(
                                    new OnAccessCompletedEventArgs(
                                    m_Result == CSLibrary.Constants.Result.OK,
                                    CSLibrary.Constants.Bank.PC,
                                    CSLibrary.Constants.TagAccess.READ,
                                    m_rdr_opt_parms.TagReadPC.pc));

                                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                */
            }
        }

        private void TagReadEPCThreadProc()
        {
            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                CurrentOperationResult = CSLibrary.Constants.Result.NO_TAG_FOUND;

                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.EPC,
                    (ushort)(EPC_START_OFFSET + m_rdr_opt_parms.TagReadEPC.offset),
                    m_rdr_opt_parms.TagReadEPC.count,
                    m_rdr_opt_parms.TagReadEPC.m_epc,
                    m_rdr_opt_parms.TagReadEPC.accessPassword,
//                    m_rdr_opt_parms.TagReadEPC.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) != true)
                    m_Result = CSLibrary.Constants.Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadEPCThreadProc()", ex);
#endif
            }
            finally
            {
                /*
                                FireAccessCompletedEvent(
                                    new OnAccessCompletedEventArgs(
                                    m_Result == CSLibrary.Constants.Result.OK,
                                    CSLibrary.Constants.Bank.EPC,
                                    CSLibrary.Constants.TagAccess.READ,
                                    m_rdr_opt_parms.TagReadEPC.epc));

                                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                */
            }
        }

        private void TagReadAccPwdThreadProc()
        {
            ushort[] readbuf = new ushort[2];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.RESERVED,
                    ACC_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    readbuf,
                    m_rdr_opt_parms.TagReadAccPwd.accessPassword,
//                    m_rdr_opt_parms.TagReadAccPwd.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadAccPwd.m_password = (uint)(readbuf[0] << 16 | readbuf[1]);
                else
                    m_Result = CSLibrary.Constants.Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadAccPwdThreadProc()", ex);
#endif
            }
            finally
            {
 /*               FireAccessCompletedEvent(
                    new OnAccessCompletedEventArgs(
                    m_Result == CSLibrary.Constants.Result.OK,
                    CSLibrary.Constants.Bank.ACC_PWD,
                    CSLibrary.Constants.TagAccess.READ,
                    m_rdr_opt_parms.TagReadAccPwd.password));

                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
*/            }
        }

        private void TagReadKillPwdThreadProc()
        {
            ushort[] readbuf = new ushort[2];

            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                //                m_Result = TagReadKillPwd(m_rdr_opt_parms.TagReadKillPwd);
                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.RESERVED,
                    KILL_PWD_START_OFFSET,
                    TWO_WORD_LEN,
                    readbuf,
                    m_rdr_opt_parms.TagReadKillPwd.accessPassword,
                    //m_rdr_opt_parms.TagReadKillPwd.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) == true)
                    m_rdr_opt_parms.TagReadKillPwd.m_password = (uint)(readbuf[0] << 16 | readbuf[1]);
                else
                    m_Result = CSLibrary.Constants.Result.FAILURE;

            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadKillPwdThreadProc()", ex);
#endif
            }
            finally
            {
                /*
                                FireAccessCompletedEvent(
                                    new OnAccessCompletedEventArgs(
                                    m_Result == CSLibrary.Constants.Result.OK,
                                    CSLibrary.Constants.Bank.KILL_PWD,
                                    CSLibrary.Constants.TagAccess.READ,
                                    m_rdr_opt_parms.TagReadKillPwd.password));

                                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                */
            }
        }

        private void TagReadTidThreadProc()
        {
            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                CurrentOperationResult = CSLibrary.Constants.Result.NO_TAG_FOUND;

                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.TID,
                    m_rdr_opt_parms.TagReadTid.offset,
                    m_rdr_opt_parms.TagReadTid.count,
                    m_rdr_opt_parms.TagReadTid.pData,
                    m_rdr_opt_parms.TagReadTid.accessPassword,
                    //m_rdr_opt_parms.TagReadTid.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) != true)
                    m_Result = CSLibrary.Constants.Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadTidThreadProc()", ex);
#endif
            }
            finally
            {
                /*
                                FireAccessCompletedEvent(
                                    new OnAccessCompletedEventArgs(
                                    m_Result == CSLibrary.Constants.Result.OK,
                                    CSLibrary.Constants.Bank.TID,
                                    CSLibrary.Constants.TagAccess.READ,
                                    m_rdr_opt_parms.TagReadTid.tid));

                                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                */
            }
        }

        private void TagReadUsrMemThreadProc()
        {
            try
            {
                FireStateChangedEvent(CSLibrary.Constants.RFState.BUSY);

                CurrentOperationResult = CSLibrary.Constants.Result.NO_TAG_FOUND;

                m_rdr_opt_parms.TagReadUser.m_pData = new UInt16[m_rdr_opt_parms.TagReadUser.count];

                m_Result = CSLibrary.Constants.Result.OK;

                if (CUST_18K6CTagRead(
                    CSLibrary.Constants.MemoryBank.USER,
                    m_rdr_opt_parms.TagReadUser.offset,
                    m_rdr_opt_parms.TagReadUser.count,
                    m_rdr_opt_parms.TagReadUser.m_pData,
                    m_rdr_opt_parms.TagReadUser.accessPassword,
                    //m_rdr_opt_parms.TagReadUser.retryCount,
                    CSLibrary.Constants.SelectFlags.SELECT) != true)
                    m_Result = CSLibrary.Constants.Result.FAILURE;
            }
            catch (System.Exception ex)
            {
#if DEBUG
//                CSLibrary.Diagnostics.CoreDebug.Logger.ErrorException("HighLevelInterface.TagReadUsrMemThreadProc()", ex);
#endif
            }
            finally
            {
                /*                FireAccessCompletedEvent(
                                    new OnAccessCompletedEventArgs(
                                    m_Result == CSLibrary.Constants.Result.OK,
                                    Bank.USER,
                                    TagAccess.READ,
                                    m_rdr_opt_parms.TagReadUser.pData));

                                FireStateChangedEvent(CSLibrary.Constants.RFState.IDLE);
                */
            }
        }
    }
}
