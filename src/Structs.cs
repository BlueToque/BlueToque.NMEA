
namespace BlueToque.NMEA
{
    /// <summary></summary>
    /// <remarks>
    /// 
    /// </remarks>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    public struct Position(double lat, double lon)
    {

        /// <summary></summary>
        public double Lat = lat;

        /// <summary></summary>
        public double Lon = lon;

        /// <summary>
        /// Is the position zero
        /// </summary>
        /// <returns></returns>
        public readonly bool IsZero() => Lat == 0 && Lon == 0;

        /// <summary>
        /// Is the position valid
        /// </summary>
        /// <returns></returns>
        public readonly bool IsValid() => Lat <= 90 && Lat >= -90 && Lon <= 180 && Lon <= 180;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"{Lat:##0.##}, {Lon:##0.##}";

    }

    /// <summary></summary>
    public struct DOP
    {
        /// <summary></summary>
        public float PDOP;

        /// <summary></summary>
        public float HDOP;

        /// <summary></summary>
        public float VDOP;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"PDOP: {PDOP} HDOP: {HDOP} VDOP: {VDOP}";
    }

    /// <summary></summary>
    public struct Satellite
    {
        /// <summary></summary>
        public int pseudoRandomCode;
        /// <summary></summary>
        public int azimuth;
        /// <summary></summary>
        public int elevation;
        /// <summary></summary>
        public int signalToNoiseRatio;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"Satellite: {pseudoRandomCode} ({azimuth}, {elevation}) SNR: {signalToNoiseRatio}";
    }
}
