public static class SpaceDistanceUtility
{
    public const double KilometersPerAstronomicalUnit = 149597870.7;

    public static string FormatKilometers(double kilometers)
    {
        double absoluteKilometers = System.Math.Abs(kilometers);
        double astronomicalUnits = kilometers / KilometersPerAstronomicalUnit;

        if (absoluteKilometers >= KilometersPerAstronomicalUnit * 0.1)
        {
            return $"{astronomicalUnits:0.00} AU";
        }

        if (absoluteKilometers >= 1000000.0)
        {
            return $"{kilometers / 1000000.0:0.0}M km";
        }

        if (absoluteKilometers >= 10000.0)
        {
            return $"{kilometers:0,0} km";
        }

        if (absoluteKilometers >= 1000.0)
        {
            return $"{kilometers:0.0} km";
        }

        return $"{kilometers:0} km";
    }
}
