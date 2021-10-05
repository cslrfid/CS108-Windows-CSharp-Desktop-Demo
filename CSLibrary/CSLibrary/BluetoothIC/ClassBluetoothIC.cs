using System;
using System.Text;

using CSLibrary.Barcode;
using CSLibrary.Barcode.Constants;
using CSLibrary.Barcode.Structures;

namespace CSLibrary
{
    public partial class BluetoothIC
    {
        string _deviceName;
        uint _firmwareVersion;

        // RFID event code
        private class DOWNLINKCMD
        {
            public static readonly byte[] GETVERSION = { 0xC0, 0x00 };
            public static readonly byte[] SETDEVICENAME = { 0xC0, 0x03 };
            public static readonly byte[] GETDEVICENAME = { 0xC0, 0x04 };
        }

        private HighLevelInterface _deviceHandler;

        internal BluetoothIC(HighLevelInterface handler)
        {
            _deviceHandler = handler;
        }

        internal bool BluetoothICPacket(byte [] recvData)
        {
            UInt16 eventCode = (UInt16)((UInt16)recvData[8] << 8 | (UInt16)recvData[9]);

            switch (eventCode)
            {
                case 0xc000:
                    if (recvData.Length == 13)
                    {
                        _firmwareVersion = (uint)((recvData[10] << 16) | (recvData[11] << 8) | (recvData[12]));
                        return true;
                    }
                    CSLibrary.Debug.WriteLine("BluetoothIC Get Version error!");
                    break;

                case 0xc001:
                    break;

                case 0xc002:
                    break;

                case 0xc003:
                    if (recvData.Length == 11)
                        if (recvData[10] == 0x00)
                            return true;
                    CSLibrary.Debug.WriteLine("Set Device Name Fail!");
                    break;

                case 0xc004:
                    _deviceName = Encoding.UTF8.GetString(recvData, 10, 21).TrimEnd((Char)0);
                    return true;
            }

            return false;
        }

        internal void Connect()
        {
            // Get Firmware Version
            _deviceHandler.SendAsync(0, 4, DOWNLINKCMD.GETVERSION, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);

            // Get Device Name
            _deviceName = null;
            _deviceHandler.SendAsync(0, 4, DOWNLINKCMD.GETDEVICENAME, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
        }

        public uint GetFirmwareVersion()
        {
            return _firmwareVersion;
        }

        public string GetDeviceName ()
        {
            return _deviceName;
        }

        public bool SetDeviceName (string deviceName)
        {
            if (deviceName.Length > 20)
                return false;

            byte[] bDeviceName = Encoding.UTF8.GetBytes(deviceName + new String('\0', 21 - deviceName.Length));
            _deviceHandler.SendAsync(0, 4, DOWNLINKCMD.SETDEVICENAME, bDeviceName, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
            _deviceName = deviceName;

            return true;
        }




        /*

                internal void SetAutoReport (bool OnOff)
                {
                    if (OnOff)
                    {
                        //_deviceHandler.SendAsync(0, 2, DOWNLINKCMD.STARTAUTOREPORTING, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.NOWAIT);
                        _deviceHandler.SendAsync(0, 2, DOWNLINKCMD.STARTAUTOREPORTING, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
                    }
                    else
                    {
                        _deviceHandler.SendAsync(0, 2, DOWNLINKCMD.STOPAUTOREPORTING, null, HighLevelInterface.BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE);
                    }
                }

                internal void DeviceRecvVoltage (uint voltagemV)
                {
                    _batteryLevel = voltagemV;

                    if (OnVoltageEvent == null)
                        return;

                    OnVoltageEvent(_deviceHandler, new Notification.VoltageEventArgs (voltagemV));
                }

                internal void DeviceRecvState(int value)
                {
                    if (OnKeyEvent == null)
                        return;

                    switch (value)
                    {
                        case 0: // button on
                            OnKeyEvent (_deviceHandler, new Notification.HotKeyEventArgs (Key.BUTTON, true));
                            break;

                        case 1: // button off
                            OnKeyEvent (_deviceHandler, new Notification.HotKeyEventArgs (Key.BUTTON, false));
                            break;
                    }
                }

                public uint GetCurrentBatteryLevel ()
                {
                    return _batteryLevel;
                }
            */

    }
}
