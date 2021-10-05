﻿using System;

using CSLibrary.Barcode;
using CSLibrary.Barcode.Constants;
using CSLibrary.Barcode.Structures;

namespace CSLibrary
{
    public partial class BarcodeReader
    {
        public enum STATE
        {
            UNKNOWN,            // unknown
            READY,              // hardware exists
            NOTVALID,           // hardware fail
            OLDVERSION          // not update 
        }

        private class DOWNLINKCMD
        {
            public static readonly byte[] BARCODEPOWERON = { 0x90, 0x00 };
            public static readonly byte[] BARCODEPOWEROFF = { 0x90, 0x01 };
            public static readonly byte[] BARCODESCANTRIGGER = { 0x90, 0x02 };
            public static readonly byte[] BARCODERAWDATA = { 0x90, 0x03 };
            public static readonly byte[] VIBRATORON = { 0x90, 0x04 };
            public static readonly byte[] VIBRATOROFF = { 0x90, 0x05 };
        }

        private HighLevelInterface _deviceHandler;

        STATE _state = STATE.NOTVALID;
        public STATE state { get { return _state; } }


        private bool _goodRead = false;
        private bool _perfix = false;
        private string _barcodeStr = "";
        readonly byte[] _barcodePrefix = new byte[] { 0x02, 0x00, 0x07, 0x10, 0x17, 0x13 };
        readonly byte[] _barcodeSuffix = new byte[] { 0x05, 0x01, 0x11, 0x16, 0x03, 0x04 };

        internal BarcodeReader(HighLevelInterface handler)
        {
            _deviceHandler = handler;
        }

        public void ClearEventHandler()
        {
            //OnCapturedNotify = delegate { };
            OnCapturedNotify = null;
        }

        /// <summary>
        /// Receive BarCode packet
        /// </summary>
        /// <param name="recvData"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        internal bool DeviceRecvData(byte[] recvData)
        {
            if (recvData[2] <= 3) // if not barcode
            {
                if (recvData[2] == 3 && recvData[10] == 0x06) // if return success
                    return true;

                return false;
            }

            if (recvData[10] == 0x02 && recvData[11] == 0x00)
            {
                // barcode perfix
                if (recvData.Length > 15 && recvData[12] == 0x07 && recvData[13] == 0x10 && recvData[14] == 0x17 && recvData[15] == 0x13)
                {
                    _barcodeStr = System.Text.Encoding.UTF8.GetString(recvData, 16, recvData[2] - 8);
                }
                else
                {
                    switch (recvData[14])
                    {
                        case 0x34: // Query
                            if (recvData.Length < 24 || recvData[15] != 0x01 || recvData[16] != 0x06 || recvData[23] != 0x01 || recvData[24] != 0x06)
                                FactoryReset();
                            //_state = STATE.OLDVERSION;
                            else
                                _state = STATE.READY;

                            break;

                        default:
                            return false;
                    }

                    return true;
                }
            }
            else
            {
                if (_barcodeStr != "")
                {
                    _barcodeStr += System.Text.Encoding.UTF8.GetString(recvData, 10, recvData[2] - 2);
                }
            }

            if (_barcodeStr.Length > 11)
                if (_barcodeStr.Substring(_barcodeStr.Length - 7) == "\u0005\u0001\u0011\u0016\u0003\u0004\u000d")
                {
                    if (OnCapturedNotify != null)
                    {
                        Barcode.Structures.DecodeMessage decodeInfo = new Barcode.Structures.DecodeMessage();       // Decode message structure.

                        decodeInfo.pchMessage = _barcodeStr.Substring(4, _barcodeStr.Length - 11);

                        FireCaptureCompletedEvent(new BarcodeEventArgs(MessageType.DEC_MSG, decodeInfo));
                    }

                    _goodRead = false;
                    _barcodeStr = "";
                }

            return true;
        }

        internal bool DeviceRecvGoodRead()
        {
            _goodRead = true;
            return true;
        }

        string GetAimID(string code)
        {
            string desc = "";
            string CodeID = code.Substring(0, 1);
            string AimID = code.Substring(1, 3);

            switch (CodeID)
            {
                case "j":
                    switch (AimID)
                    {
                        case "]C0":
                            break;
                    }
                    break;

                case "f":
                    break;

                case "d":
                    break;

                case "c":
                    break;

                case "e":
                    break;

                case "v":
                    break;

                case "b":
                    break;

                case "a":
                    break;

                case "i":
                    break;

                case "H":
                    break;

                case "R":
                    break;

                case "y":
                    break;

                case "B":
                    break;

                case "n":
                    break;

                //case "v":
                //    break;

                case "I":
                    break;

                case "D":
                    break;

                //case "f":
                //    break;

                case "s":
                    break;

                //case "n":
                //    break;

                case "p":
                    break;

                case "m":
                    break;

                case "r":
                    break;

                //case "s":
                //    break;

                case "Q":
                    break;

                case "z":
                    break;

                case "u":
                    break;

                case "x":
                    break;

                case "h":
                    break;
            }

            return desc;
        }



        public event EventHandler<CSLibrary.Barcode.BarcodeEventArgs> OnCapturedNotify;

        private BarcodeState m_state = BarcodeState.IDLE;
        // Helper for marshalling execution to GUI thread
        private object synlock = new object();

        private void TellThemCaptureCompleted(BarcodeEventArgs e)
        {
            if (OnCapturedNotify != null)
            {
                OnCapturedNotify(_deviceHandler, e);
            }
        }

        private void FireCaptureCompletedEvent(BarcodeEventArgs e)
        {
            TellThemCaptureCompleted(e);
        }


        /// <summary>
        /// Start to capture barcode, until stop is sent.
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            try
            {
                if (_state == STATE.NOTVALID)
                    return false;

                _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_ContinueMode, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);

                _goodRead = false;
                _barcodeStr = "";
            }
            catch (System.Exception ex)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stop capturing
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            bool rc = true;

            try
            {
                _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_StopContinue, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            }
            catch (System.Exception ex)
            {
                rc = false;
            }
            return rc;
        }

        public bool VibratorOn()
        {
            if (_state != STATE.READY)
                return false;

            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.VIBRATORON, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            return true;
        }

        public bool VibratorOff()
        {
            if (_state != STATE.READY)
                return false;

            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.VIBRATOROFF, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.VIBRATOROFF, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            return true;
        }

        readonly byte[] barcodecmd_TriggerMode = new byte[] { 0x1b, 0x31 };     // Start Trigger Mode
        readonly byte[] barcodecmd_ContinueMode = new byte[] { 0x1b, 0x33 };    // Start Continue Scan Mode
        readonly byte[] barcodecmd_StopContinue = new byte[] { 0x1b, 0x30 };    // Stop Continue Scan Mode
        readonly byte[] barcodecmd_SysModeEnter = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x30, 0x30, 0x36, 0x30, 0x31, 0x30, 0x3b };  // Enter Engineer Mode
        readonly byte[] barcodecmd_PermContinueMode = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x32, 0x30, 0x32, 0x30, 0x3b };  
        readonly byte[] barcodecmd_PermTriggerMode = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x32, 0x30, 0x30, 0x30, 0x3b };
        readonly byte[] barcodecmd_ScanCycleTime30000 = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x31, 0x33, 0x30, 0x30, 0x30, 0x3d, 0x33, 0x30, 0x30, 0x30, 0x30, 0x3b };
        readonly byte[] barcodecmd_SysModeExit = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x30, 0x30, 0x36, 0x30, 0x30, 0x30, 0x3b };   // Exit Engineer Mode
        readonly byte[] barcodecmd_QueryESN = new byte[] { 0x7e, 0x00, 0x00, 0x05, 0x33, 0x48, 0x30, 0x32, 0x30, 0xB3 };
        readonly byte[] barcodecmd_QueryPrefix = new byte[] { 0x7e, 0x00, 0x00, 0x02, 0x33, 0x37, 0xf9 };    // Query Prefix and Suffix

        readonly byte[] barcodecmd_EnableAllPrefixSuffix    = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x31, 0x31, 0x30, 0x31, 0x30, 0x3b };
        readonly byte[] barcodecmd_SelfPrefixCodeIdAimId    = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x31, 0x37, 0x30, 0x34, 0x30, 0x3b };
        readonly byte[] barcodecmd_EnableSelfPrefix         = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x35, 0x30, 0x31, 0x30, 0x3b };
        readonly byte[] barcodecmd_SetSelfPrefix            = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x30, 0x30, 0x30, 0x30, 0x20, 0x3d, 0x20, 0x30, 0x78, 0x30, 0x32, 0x30, 0x30, 0x30, 0x37, 0x31, 0x30, 0x31, 0x37, 0x31, 0x33, 0x3b };
        readonly byte[] barcodecmd_EnableSelfSuffix         = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x36, 0x30, 0x31, 0x30, 0x3b };
        readonly byte[] barcodecmd_SetSelfSuffix            = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x31, 0x30, 0x30, 0x30, 0x20, 0x3d, 0x20, 0x30, 0x78, 0x30, 0x35, 0x30, 0x31, 0x31, 0x31, 0x31, 0x36, 0x30, 0x33, 0x30, 0x34, 0x3b };
        readonly byte[] barcodecmd_EnableAimId              = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x38, 0x30, 0x33, 0x30, 0x3b };
        readonly byte[] barcodecmd_EnableCodeId             = new byte[] { 0x6e, 0x6c, 0x73, 0x30, 0x33, 0x30, 0x37, 0x30, 0x31, 0x30, 0x3b };

        internal void CheckHWValid()
        {
            //var a = GetLRC(new byte[] { 0x48, 0x30, 0x32, 0x30 });
            //var b = GetLRC(new byte[] { 0x37 });

            _state = STATE.NOTVALID;
            //_deviceHandler.SendAsync(CSLibrary.HighLevelInterface.DEVICEID.Barcode, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_QueryESN, CSLibrary.HighLevelInterface.BTCOMMANDTYPE.Validate, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1);
            _deviceHandler.SendAsync(CSLibrary.HighLevelInterface.DEVICEID.Barcode, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_QueryPrefix, CSLibrary.HighLevelInterface.BTCOMMANDTYPE.Validate, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.DATA1);
        }

        // public barcode function
        public void FactoryReset()
        {
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_SysModeEnter, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_ScanCycleTime30000, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_PermTriggerMode, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_EnableAllPrefixSuffix, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_SelfPrefixCodeIdAimId, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_EnableSelfPrefix, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_SetSelfPrefix, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_EnableSelfSuffix, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_SetSelfSuffix, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_EnableAimId, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_EnableCodeId, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);
            _deviceHandler.SendAsync(0, 1, DOWNLINKCMD.BARCODERAWDATA, barcodecmd_SysModeExit, CSLibrary.HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE_DATA1);

            CheckHWValid();
        }

        // LRC : Data checkout value 1 bytes(Computing method: 0xff^lens^types^data
        Byte GetLRC (Byte [] data)
        {
            int lrc = 0xff ^ (data.Length + 1) ^ 0x33;

            for (int cnt = 0; cnt < data.Length; cnt++)
                lrc ^= data[cnt];

            return (Byte)lrc;
        }

    }
}
