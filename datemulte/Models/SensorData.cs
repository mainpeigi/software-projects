using CsvHelper.Configuration.Attributes;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using CsvHelper;

namespace Datemulte_2.Models;
/// <summary>
/// Represents data from flow measurement devices
/// Contains 40+ properties for various readings including flow, temperature, pressure, and accelerometer data
/// Uses SafeDoubleConverter to handle malformed or missing numeric values gracefully
/// </summary>
public class SensorData
{
    // Metadata fields
    public string Block { get; set; } = "";
    public string Alloc { get; set; } = "";
    public string Time_Unix { get; set; } = "";

    // Time measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Time_Count { get; set; }
    public string Time { get; set; } = "";

    // Flow measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Flow { get; set; }

    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Flow80 { get; set; }

    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Flow120 { get; set; }

    // Ultrasonic sensor measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Nano80 { get; set; }

    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Nano120 { get; set; }

    // Temperatire measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Temp1 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Temp2 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double TempPCB { get; set; }

    // Pressure measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Press1 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Press2 { get; set; }

    // Volumne and mass measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Volume { get; set; }

    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Mass { get; set; }

    // Signal amplitude measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double amplUp80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double amplUp120 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double amplDo80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double amplDo120 { get; set; }

    // Pulse width measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double pwUp80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double pwUp120 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double pwDo80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double pwDo120 { get; set; }

    // Quality/averaging metrics
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double qtyAvg80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double qtyAvg120 { get; set; }

    // Accelerometer data (3-axis)
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double AccX { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double AccY { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double AccZ { get; set; }

    // Power supply voltages
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double VIN { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double VCONS { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double VBAT { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double VSW { get; set; }

    // System load metrics
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Load { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Idle { get; set; }

    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Transit80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Transit120 { get; set; }

    // Density and cumulative measurements
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Density { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double SumMassFlow80 { get; set; }
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double SumMassFlow120 { get; set; }

    // Error tracking
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Error { get; set; }

    // Calculated averages
    [Name("Flow AVG")]
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Flow_AVG { get; set; }
    [Name("Flow80")]
    [TypeConverter(typeof(SafeDoubleConverter))]
    public double Flow80_End { get; set; }
}

/// <summary>
/// Custom type converter for safely parsing double values from CSV files
/// Returns 0.0 for null, or invalid numeric values instead of throwinge exceptions
/// Handles common numeric placeholders: "", ",", "N/A", "NULL", "NaN"
/// </summary>
public class SafeDoubleConverter : DefaultTypeConverter
{
    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        // Handle null or empty
        if (string.IsNullOrWhiteSpace(text)) return 0.0;

        text = text.Trim();

        // Handle common placeholders for missing data
        if (text == "" || text == "," || text == "N/A" || text == "NULL" || text == "NaN")
            return 0.0;
        
        // Attempt to parse using invariant culture
        if(double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        // Fallback: return 0.0 for any unparseable values
        return 0.0;
    }
}