﻿using System;

using CSLibrary.Barcode;
using CSLibrary.Barcode.Constants;
using CSLibrary.Barcode.Structures;

namespace CSLibrary
{
    public partial class SiliconLabIC
    {
        public event EventHandler<CSLibrary.SiliconLabIC.Events.OnAccessCompletedEventArgs> OnAccessCompleted;

        uint _firmwareVersion;
        public bool _firmwareOlderT108 = false;
        string _serailNumber = null;
        string _PcbVersion;

        // RFID event code
        private class DOWNLINKCMD
		{
			public static readonly byte[] GETVERSION = { 0xB0, 0x00 };
            public static readonly byte[] GETSERIALNUMBER = { 0xB0, 0x04 };
        }

        private HighLevelInterface _deviceHandler;

        internal SiliconLabIC(HighLevelInterface handler)
		{
			_deviceHandler = handler;
        }

        internal void Connect ()
        {
            //internal void GetVersion()
            //{
            _deviceHandler.SendAsync(0, 3, DOWNLINKCMD.GETVERSION, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            //}

            //internal void GetSerialNumber()
            //{
            _deviceHandler.SendAsync(0, 3, DOWNLINKCMD.GETSERIALNUMBER, new byte[1], HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            //}
        }

        internal HighLevelInterface.BTWAITCOMMANDRESPONSETYPE ProcessDataPacket (byte [] data)
        {
            uint pktType = (uint)(data[8] << 8 | data[9]);

            switch (pktType)
            {
                case 0xb000:    // version
                    if (data.Length >= 12)
                    {
                        _firmwareVersion = (uint)((data[10] << 16) | (data[11] << 8) | (data[12]));
                        if (_firmwareVersion < 0x00010008)
                            _firmwareOlderT108 = true;
                    }
                    return HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.BTAPIRESPONSE;

                case 0xb004:    // serial number
                                //_serailNumber = System.Text.Encoding.UTF8.GetString(data, 10, 13);
                                //_PcbVersion = (uint)(((data[23] - 0x30) << 16) | ((data[24] - 0x30) << 8) | (data[25] - 0x30));
                                //_PcbVersion = (uint)(((((data[23] & 0x0f) << 8) * 10) + ((data[24] &0x0f) << 8)) | (data[25] - 0x30));
                    try
                    {
                        _serailNumber = System.Text.Encoding.UTF8.GetString(data, 10, 13);
                    }
                    catch (Exception ex)
                    {
                        _serailNumber = "";

                    }

                    try
                    {
                        if (data[25] == 0x00)
                            data[25] = 0x30;
                        _PcbVersion = System.Text.Encoding.UTF8.GetString(data, 23, 3);
                    }
                    catch (Exception ex)
                    {
                        _PcbVersion = "";
                    }

                    /*
                    if (OnAccessCompleted != null)
                    {
                        try
                        {
                            Events.OnAccessCompletedEventArgs args = new Events.OnAccessCompletedEventArgs(_serailNumber, Constants.AccessCompletedCallbackType.SERIALNUMBER);

                            OnAccessCompleted(this, args);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    */

                    return HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.BTAPIRESPONSE;
            }

            return 0;
        }

        public UInt32 GetFirmwareVersion ()
        {
            return _firmwareVersion;
        }

        public void GetSerialNumber()
        {
            if (OnAccessCompleted != null)
            {
                try
                {
                    Events.OnAccessCompletedEventArgs args = new Events.OnAccessCompletedEventArgs(_serailNumber, Constants.AccessCompletedCallbackType.SERIALNUMBER);

                    OnAccessCompleted(this, args);
                }
                catch (Exception ex)
                {
                }
            }
        }

        public string GetSerialNumberSync()
        {
            return _serailNumber;
        }

        public string GetPCBVersion ()
        {
            return _PcbVersion;
        }

        public void ClearEventHandler()
        {
            OnAccessCompleted = delegate { };
        }
    }
}
