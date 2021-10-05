using System;

namespace CSLibrary
{
    public partial class HighLevelInterface
    {
        private void FireReaderStateChangedEvent(CSLibrary.Events.OnReaderStateChangedEventArgs args)
        {
            if (OnReaderStateChanged != null)
            {
                try
                {
                    OnReaderStateChanged(this, args);
                }
                catch (Exception ex)
                {
                    //CSLibrary.Debug.PrintLine("Communication retry fail!!");

                    //Console.WriteLine(ex);
                }
            }
        }
    }
}
