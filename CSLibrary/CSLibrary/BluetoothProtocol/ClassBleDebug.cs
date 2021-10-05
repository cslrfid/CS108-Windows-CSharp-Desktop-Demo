using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSLibrary
{
    static public class InventoryDebug
    {
        static public uint _inventoryPacketCount = 0;
        static public uint _inventorySkipPacketCount = 0;

        static public void Clear()
        {
            _inventoryPacketCount = 0;
            _inventorySkipPacketCount = 0;
            Print();
        }

        static public void InventoryPackerCountInc ()
        {
            _inventoryPacketCount++;
            Print();
        }

        static public void InventorySkipPackerAdd(uint cnt)
        {
            _inventorySkipPacketCount += cnt;
            Print();
        }

        static public void Print ()
        {
            CSLibrary.Debug.WriteLine("BLE stat : Total Inventory Received {0}, Skip packet {1}", _inventoryPacketCount, _inventorySkipPacketCount);
        }

    }
}
