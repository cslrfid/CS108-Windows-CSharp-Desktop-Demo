using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    public partial class HighLevelInterface
    {
        byte[] _recvBuffer = new byte[8 + 255 + 20]; // receive packet buffer
        int _currentRecvBufferSize = 0;
        byte[] _recvBufferBackup = new byte[8 + 255 + 20]; // backup receive packet buffer
        int _currentRecvBufferSizeBackup = 0;

        private void CharacteristicOnValueUpdated(byte [] recvData)
        {
            if (CheckSingalPacket(recvData))
            {
                return;
            }

            // First Method
            if (FirstAssemblePacketMohod(recvData) || BackupAssemblePacketMohod(recvData))
            {
                _currentRecvBufferSize = 0;
                _currentRecvBufferSizeBackup = 0;
            }
        }

        byte _blePacketRunningNumber = 0x82;

        bool CheckAPIHeader(byte[] data)
        {
            return (data[0] == 0xa7 &&
                    data[1] == 0xb3 &&
                    data[2] <= 120 &&
                    // data[4] == 0x82 &&
                    data[5] == 0x9e &&
                    (data[3] == 0xc2 ||
                     data[3] == 0x6a ||
                     data[3] == 0xd9 ||
                     data[3] == 0xe8 ||
                     data[3] == 0x5f)
                    );
        }

        bool CheckSingalPacket(byte[] data)
        {
            if (!CheckAPIHeader(data) || data[2] != (data.Length - 8))
                return false;

            UInt16 recvCRC = (UInt16)(data[6] << 8 | data[7]);
            if (recvCRC != Tools.Crc.ComputeChecksum(data))
                return false;

            ProcessAPIPacket(data);
            return true;
        }

        bool FirstAssemblePacketMohod(byte[] recvData)
        {
            if (CheckAPIHeader(recvData))
            {
                if (_currentRecvBufferSize > 0)
                {
                    CSLibrary.Debug.WriteLine("BT1 : Packet Too small, can not process");
                }

                Array.Copy(recvData, 0, _recvBuffer, 0, recvData.Length);
                _currentRecvBufferSize = recvData.Length;
                return false;
            }

            if ((_currentRecvBufferSize + recvData.Length) > _recvBuffer[2] + 8)
            {
                CSLibrary.Debug.WriteLine("BT1 : Current packet size too large");
                _currentRecvBufferSize = 0;
                return false;
            }

            Array.Copy(recvData, 0, _recvBuffer, _currentRecvBufferSize, recvData.Length);
            _currentRecvBufferSize += recvData.Length;

            if (_currentRecvBufferSize == (_recvBuffer[2] + 8))
            {
                UInt16 recvCRC = (UInt16)(_recvBuffer[6] << 8 | _recvBuffer[7]);
                UInt16 calCRC = Tools.Crc.ComputeChecksum(_recvBuffer);
                if (recvCRC != calCRC)
                {
                    CSLibrary.Debug.WriteLine("BT1 : Checksum error " + recvCRC.ToString("X4") + " " + calCRC.ToString("X4"));
                    _currentRecvBufferSize = 0;
                    return false;
                }

                ProcessAPIPacket(_recvBuffer);
                return true;
            }

            return false;
        }

        bool BackupAssemblePacketMohod(byte[] recvData)
        {
            if (_currentRecvBufferSizeBackup == 0)
            {
                if (!CheckAPIHeader(recvData))
                    return false;

                Array.Copy(recvData, 0, _recvBufferBackup, 0, recvData.Length);
                _currentRecvBufferSizeBackup = recvData.Length;
                return false;
            }

            if ((_currentRecvBufferSizeBackup + recvData.Length) > _recvBuffer[2] + 8)
            {
                CSLibrary.Debug.WriteLine("BT2 : Current packet size too large");
                _currentRecvBufferSizeBackup = 0;
                return false;
            }

            Array.Copy(recvData, 0, _recvBufferBackup, _currentRecvBufferSizeBackup, recvData.Length);
            _currentRecvBufferSizeBackup += recvData.Length;

            if (_currentRecvBufferSizeBackup == (_recvBuffer[2] + 8))
            {
                UInt16 recvCRC = (UInt16)(_recvBufferBackup[6] << 8 | _recvBufferBackup[7]);
                UInt16 calCRC = Tools.Crc.ComputeChecksum(_recvBuffer);
                if (recvCRC != calCRC)
                {
                    CSLibrary.Debug.WriteLine("BT2 : Checksum error " + recvCRC.ToString("X4") + " " + calCRC.ToString("X4"));
                    _currentRecvBufferSizeBackup = 0;
                    return false;
                }

                ProcessAPIPacket(_recvBufferBackup);
                return true;
            }

            return false;
        }

    }
}