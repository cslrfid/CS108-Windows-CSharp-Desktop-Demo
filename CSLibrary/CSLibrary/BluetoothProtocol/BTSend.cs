using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public partial class HighLevelInterface
    {
        readonly byte[] destinationsID = { 0xc2, 0x6a, 0xd9, 0xe8, 0x5f };

        internal enum BTCOMMANDTYPE
        {
            Normal,         // Normanl command : send 5 times, and clear all command if send fail
            Validate,       // Validate command : for test hardhware, send 2 times
            None            // end of enum
        }

        internal enum DEVICEID : byte
        {
            RFID = 0xc2,
            Barcode = 0x6a,
            Notification = 0xd9,
            SiliconLabIC = 0xe8,
            NluetoothIC = 0x5f
        }

        [Flags]
        internal enum BTWAITCOMMANDRESPONSETYPE
        {
            NOWAIT = 0,
            BTAPIRESPONSE = 1,
            COMMANDENDRESPONSE = 2,
            DATA1 = 4,
            DATA2 = 8,
            REGISTERRETURN = 16,
            ABORTRESPONSE = 32,
            TAGACCESSPACKET = 64,

            WAIT_BTAPIRESPONSE = BTAPIRESPONSE,
            WAIT_BTAPIRESPONSE_COMMANDENDRESPONSE = BTAPIRESPONSE | COMMANDENDRESPONSE,
            WAIT_BTAPIRESPONSE_DATA1 = BTAPIRESPONSE | DATA1,
            WAIT_BTAPIRESPONSE_DATA2 = BTAPIRESPONSE | DATA2,
            WAIT_BTAPIRESPONSE_DATA1_COMMANDENDRESPONSE = BTAPIRESPONSE | DATA1 | COMMANDENDRESPONSE,
            WAIT_BTAPIRESPONSE_DATA2_COMMANDENDRESPONSE = BTAPIRESPONSE | DATA2 | COMMANDENDRESPONSE,
        }

        internal class SENDBUFFER
        {
            public BTCOMMANDTYPE type = BTCOMMANDTYPE.None;     // command type
            public byte[] packetData;                           // send data packet
            public UInt32 cmdRemark;                            // reserver
            public BTWAITCOMMANDRESPONSETYPE dataRemark;        // BT command return data check
            public Action sendFailCallback = null;              // Send fail callback
        }

        private List<SENDBUFFER> _sendBuffer = new List<SENDBUFFER>();
        //private bool _PROTOCOL_HardwareDiagnosticsMode = true;
        private uint _PROTOCOL_RetryCount = 0;

        private object _bleEngineLock = new object();
        private DateTime _packetResponseTimeout;
        private DateTime _packetDelayTimeout;

        internal UInt32 _currentCmdRemark;
        private BTWAITCOMMANDRESPONSETYPE _NeedCommandResponseType;
        private BTWAITCOMMANDRESPONSETYPE _currentCommandResponse;

        #region ---- Internal function ----

        internal bool SendAsync(int connection, int destination, byte[] eventCode = null, byte[] payload = null, BTWAITCOMMANDRESPONSETYPE sendRemark = BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE, UInt32 cmdRemark = 0xffffffff)
        {
            byte[] sendData;

            if (eventCode == null && payload == null)
            {
                sendData = new byte[8];

                sendData[6] = 0x00;
                sendData[7] = 0x00;
            }
            else if (payload == null)
            {
                if (eventCode.Length > (255 - 8))
                    return false;

                sendData = new byte[8 + eventCode.Length];

                Array.Copy(eventCode, 0, sendData, 8, eventCode.Length);

                sendData[2] = (byte)eventCode.Length;
            }
            else
            {
                if ((eventCode.Length + payload.Length) > (255 - 8))
                    return false;

                sendData = new byte[8 + eventCode.Length + payload.Length];

                Array.Copy(eventCode, 0, sendData, 8, eventCode.Length);
                Array.Copy(payload, 0, sendData, 8 + eventCode.Length, payload.Length);

                sendData[2] = (byte)(eventCode.Length + payload.Length);
            }

            sendData[0] = 0xa7;
            sendData[1] = (byte)((connection == 0) ? 0xb3 : 0xe6);
            sendData[3] = destinationsID[destination];
            sendData[4] = 0x82;
            sendData[5] = 0x37; // downlink
            sendData[6] = 0x00;
            sendData[7] = 0x00;

            SENDBUFFER sendItem = new SENDBUFFER();
            sendItem.packetData = sendData;
            sendItem.cmdRemark = cmdRemark;
            sendItem.dataRemark = sendRemark;

            _sendBuffer.Add(sendItem);
            BLERWEngineTimer();

            return true;
        }

        internal bool SendAsync(DEVICEID destination, 
                                    byte[] eventCode = null, 
                                    byte[] payload = null, 
                                    BTCOMMANDTYPE type = BTCOMMANDTYPE.Normal, 
                                    BTWAITCOMMANDRESPONSETYPE sendRemark = BTWAITCOMMANDRESPONSETYPE.WAIT_BTAPIRESPONSE, 
                                    UInt32 cmdRemark = 0xffffffff, 
                                    Action failCallback = null)
        {
            byte[] sendData;

            if (eventCode == null && payload == null)
            {
                sendData = new byte[8];

                sendData[6] = 0x00;
                sendData[7] = 0x00;
            }
            else 
            {
                if (eventCode.Length > (255 - 8))
                    return false;

                if (payload == null)
                {
                    sendData = new byte[8 + eventCode.Length];

                    Array.Copy(eventCode, 0, sendData, 8, eventCode.Length);

                    sendData[2] = (byte)eventCode.Length;
                }
                else
                {
                    if ((eventCode.Length + payload.Length) > (255 - 8))
                        return false;

                    sendData = new byte[8 + eventCode.Length + payload.Length];

                    Array.Copy(eventCode, 0, sendData, 8, eventCode.Length);
                    Array.Copy(payload, 0, sendData, 8 + eventCode.Length, payload.Length);

                    sendData[2] = (byte)(eventCode.Length + payload.Length);
                }
            }

            sendData[0] = 0xa7;
            sendData[1] = 0xb3; // (byte)((connection == 0) ? 0xb3 : 0xe6);
            sendData[3] = (byte)destination;
            sendData[4] = 0x82;
            sendData[5] = 0x37;
            sendData[6] = 0x00;
            sendData[7] = 0x00;

            SENDBUFFER sendItem = new SENDBUFFER();
            sendItem.type = type;
            sendItem.packetData = sendData;
            sendItem.cmdRemark = cmdRemark;
            sendItem.dataRemark = sendRemark;
            sendItem.sendFailCallback = failCallback;

            _sendBuffer.Add(sendItem);
            BLERWEngineTimer();

            return true;
        }

        async void BLERWEngineTimer()
        {
            await Task.Delay(10);

            lock (_bleEngineLock)
            {
                if (_NeedCommandResponseType != BTWAITCOMMANDRESPONSETYPE.NOWAIT)
                {
                    CSLibrary.Debug.WriteLine("wait response : " + _NeedCommandResponseType.ToString() + ":" + _currentCommandResponse);
                    if ((_currentCommandResponse & _NeedCommandResponseType) == _NeedCommandResponseType)
                    {
                        if (_sendBuffer.Count > 0)
                            _sendBuffer.RemoveAt(0);

                        _NeedCommandResponseType = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                        _PROTOCOL_RetryCount = 0;
                    }
                    else if (DateTime.Now > _packetResponseTimeout)
                    {
                        switch (_sendBuffer[0].type)
                        {
                            case BTCOMMANDTYPE.None:
                            case BTCOMMANDTYPE.Normal:
                                if (_PROTOCOL_RetryCount > 14) // retry 14 times (~28s)
                                {
                                    // cancel all command and send error event

                                    CSLibrary.Debug.WriteLine("Communication retry fail!!");
                                    _NeedCommandResponseType = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                                    FireReaderStateChangedEvent(new Events.OnReaderStateChangedEventArgs(_sendBuffer[0], Constants.ReaderCallbackType.COMMUNICATION_ERROR));
                                    _sendBuffer.Clear();
                                    _PROTOCOL_RetryCount = 0;
                                }
                                else
                                {
                                    _PROTOCOL_RetryCount++;
                                    CSLibrary.Debug.WriteLine("Command timeout");
                                    _NeedCommandResponseType = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                                }
                                break;

                            case BTCOMMANDTYPE.Validate:
                                if (_PROTOCOL_RetryCount > 0) // retry 1 times
                                {
                                    // cancel all command and send error event

                                    CSLibrary.Debug.WriteLine("hardware fail!!");

                                    if (_sendBuffer[0].sendFailCallback != null)
                                        _sendBuffer[0].sendFailCallback();
                                    _NeedCommandResponseType = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                                    _sendBuffer.RemoveAt(0);
                                    _PROTOCOL_RetryCount = 0;
                                }
                                else
                                {
                                    _PROTOCOL_RetryCount++;
                                    CSLibrary.Debug.WriteLine("Hardware vaildate command timeout");
                                    _NeedCommandResponseType = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                                }
                                break;

                        }

                    }
                }

                if (_NeedCommandResponseType == BTWAITCOMMANDRESPONSETYPE.NOWAIT)
                {
                    if (_sendBuffer.Count > 0 && DateTime.Now > _packetDelayTimeout)
                    {
                        _currentCommandResponse = BTWAITCOMMANDRESPONSETYPE.NOWAIT;
                        _currentCmdRemark = _sendBuffer[0].cmdRemark;
                        _NeedCommandResponseType = _sendBuffer[0].dataRemark;
                        BLE_Send(_sendBuffer[0].packetData);
                        _packetDelayTimeout = DateTime.Now;
                        _packetResponseTimeout = DateTime.Now.AddSeconds(2);

                        if (_currentCommandResponse == BTWAITCOMMANDRESPONSETYPE.NOWAIT)
                        {
                            if (_sendBuffer[0].packetData[2] == 0x02 && _sendBuffer[0].packetData[9] == 0x00)
                            {
                                switch (_sendBuffer[0].packetData[8])
                                {
                                    case 0x80:
                                        _packetDelayTimeout = DateTime.Now.AddSeconds(3);
                                        break;

                                    case 0x90:
                                        //_packetDelayTimeout = DateTime.Now.AddSeconds(1);
                                        break;
                                }
                            } // barcode command delay
                            else if (_sendBuffer[0].packetData[8] == 0x90 && _sendBuffer[0].packetData[9] == 0x03)
                            {
                                _packetDelayTimeout = DateTime.Now.AddMilliseconds(500);
                            }
                        }

                        CSLibrary.Debug.WriteBytes("BT send data ("+ _sendBuffer[0].dataRemark.ToString()+")", _sendBuffer[0].packetData);
                    }
                }

                if (_sendBuffer.Count == 0)
                    ExecuteFinishBLETask();
            }
        }

        #endregion

        #region ---- Public function ----

        public bool BLEBusy
        {
            get { return (_sendBuffer.Count != 0); }
        }

        #endregion

    }
}
