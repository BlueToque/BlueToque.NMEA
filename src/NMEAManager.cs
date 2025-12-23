using System.Threading;

namespace BlueToque.NMEA
{
    /// <summary>
    /// The NMEA Manager reads NMEA sentences from an attached GPS Receiver
    /// </summary>
    public static class NMEAManager
    {
        static NMEAManager() => Interpreter = new Interpreter();

        /// <summary>
        /// The instance of the NMEA Interpreter
        /// </summary>
        public static Interpreter Interpreter { get; internal set; }

        /// <summary>
        /// These are the baud rates to test for the GPS
        /// </summary>
        public static readonly uint[] BaudRates = [4800, 9600];

        static readonly AutoResetEvent m_event = new(false);
    }

    /// <summary> </summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="name"></param>
    /// <param name="baud"></param>
    public class PortType(string name, uint baud = 9600)
    {
        /// <summary>Port name (COM1) </summary>
        public string Name = name;

        /// <summary> Port bitrate </summary>
        public uint Baud = baud;

        /// <summary> </summary>
        /// <returns></returns>
        public override string ToString() => $"{Name} ({Baud})";
    }


}
