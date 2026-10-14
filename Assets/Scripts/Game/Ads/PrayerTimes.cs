using System;
using System.Collections.Generic;
using System.Globalization;

namespace KarachiTycoon.Game.Ads
{
    // Real-world prayer times and Islamic calendar blackouts on the player's own clock (design doc §19, §22):
    // no ads around the five prayers, around iftar in Ramadan, or on 9-10 Muharram.
    // Times come from the sun's position for a location guessed from the device time zone
    // (the target markets are listed; anything else falls back to the zone's longitude).
    // Plain C# (no UnityEngine).
    public class PrayerTimes
    {
        public readonly double Lat, Lng;

        static readonly Dictionary<string, (double lat, double lng)> Zones = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Asia/Karachi"] = (24.86, 67.01), ["Pakistan Standard Time"] = (24.86, 67.01),
            ["America/Toronto"] = (43.65, -79.38), ["America/New_York"] = (40.71, -74.01), ["Eastern Standard Time"] = (43.65, -79.38),
            ["America/Vancouver"] = (49.28, -123.12), ["America/Los_Angeles"] = (34.05, -118.24), ["Pacific Standard Time"] = (49.28, -123.12),
            ["America/Edmonton"] = (51.05, -114.07), ["Mountain Standard Time"] = (51.05, -114.07),
            ["America/Winnipeg"] = (49.9, -97.14), ["America/Chicago"] = (41.88, -87.63), ["Central Standard Time"] = (43.65, -79.38),
            ["America/Halifax"] = (44.65, -63.57), ["Atlantic Standard Time"] = (44.65, -63.57),
            ["Asia/Dubai"] = (25.2, 55.27), ["Arabian Standard Time"] = (25.2, 55.27),
            ["Europe/London"] = (51.51, -0.13), ["GMT Standard Time"] = (51.51, -0.13),
        };

        public PrayerTimes(double lat, double lng) { Lat = lat; Lng = lng; }

        public static PrayerTimes ForLocalZone()
        {
            var tz = TimeZoneInfo.Local;
            if (Zones.TryGetValue(tz.Id, out var p)) return new PrayerTimes(p.lat, p.lng);
            return new PrayerTimes(30, tz.BaseUtcOffset.TotalHours * 15);
        }

        // Fajr, Dhuhr, Asr (Shafi), Asr (Hanafi), Maghrib, Isha as local times; NaN where the sun never gets there.
        public double[] Hours(DateTime localDate, double utcOffsetHours)
        {
            double jd = Julian(localDate.Date) - Lng / 360.0;
            var (decl, eqt) = Sun(jd + 0.5);
            double dhuhr = 12 + utcOffsetHours - Lng / 15 - eqt;
            double H(double altitude)
            {
                double cos = (Math.Sin(Rad(altitude)) - Math.Sin(Rad(Lat)) * Math.Sin(Rad(decl))) / (Math.Cos(Rad(Lat)) * Math.Cos(Rad(decl)));
                return cos < -1 || cos > 1 ? double.NaN : Deg(Math.Acos(cos)) / 15;
            }
            double Asr(int factor) => dhuhr + H(Deg(Math.Atan(1 / (factor + Math.Tan(Rad(Math.Abs(Lat - decl)))))));
            return new[] { dhuhr - H(-18), dhuhr, Asr(1), Asr(2), dhuhr + H(-0.833), dhuhr + H(-18) };
        }

        // Why ads are blocked right now, or null when they are allowed.
        public string Blackout(DateTime now, AdConfig cfg, double? utcOffsetHours = null)
        {
            double off = utcOffsetHours ?? TimeZoneInfo.Local.GetUtcOffset(now).TotalHours;
            double h = now.TimeOfDay.TotalHours, w = cfg.PrayerBlackoutMinutes / 60.0;
            var t = Hours(now, off);
            bool Near(double p, double before, double after) => !double.IsNaN(p) && h >= p - before && h <= p + after;
            if (Near(t[0], w, w) || Near(t[1], w, w) || (!double.IsNaN(t[2]) && h >= t[2] - w && h <= (double.IsNaN(t[3]) ? t[2] : t[3]) + w)
                || Near(t[4], w, w) || Near(t[5], w, w)) return "prayer";
            var (month, day) = Hijri(now);
            if (cfg.RamadanIftarBlackout && month == 9 && Near(t[4], cfg.IftarBlackoutBeforeMinutes / 60.0, cfg.IftarBlackoutAfterMinutes / 60.0)) return "iftar";
            if (cfg.MuharramBlackout && month == 1 && (day == 9 || day == 10)) return "muharram";
            return null;
        }

        public static (int month, int day) Hijri(DateTime d)
        {
            try { var c = new UmAlQuraCalendar(); return (c.GetMonth(d), c.GetDayOfMonth(d)); }
            catch { var c = new HijriCalendar(); return (c.GetMonth(d), c.GetDayOfMonth(d)); }
        }

        // ---------- solar maths (after praytimes.org) ----------
        static double Rad(double d) => d * Math.PI / 180;
        static double Deg(double r) => r * 180 / Math.PI;
        static double Fix(double a, double b) => a - b * Math.Floor(a / b);

        static double Julian(DateTime d)
        {
            int y = d.Year, m = d.Month;
            if (m <= 2) { y -= 1; m += 12; }
            double a = Math.Floor(y / 100.0), b = 2 - a + Math.Floor(a / 4);
            return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d.Day + b - 1524.5;
        }

        static (double decl, double eqt) Sun(double jd)
        {
            double D = jd - 2451545.0;
            double g = Fix(357.529 + 0.98560028 * D, 360), q = Fix(280.459 + 0.98564736 * D, 360);
            double L = Fix(q + 1.915 * Math.Sin(Rad(g)) + 0.020 * Math.Sin(Rad(2 * g)), 360);
            double e = 23.439 - 0.00000036 * D;
            double ra = Fix(Deg(Math.Atan2(Math.Cos(Rad(e)) * Math.Sin(Rad(L)), Math.Cos(Rad(L)))) / 15, 24);
            double decl = Deg(Math.Asin(Math.Sin(Rad(e)) * Math.Sin(Rad(L))));
            double eqt = q / 15 - ra;
            if (eqt > 12) eqt -= 24;
            if (eqt < -12) eqt += 24;
            return (decl, eqt);
        }
    }
}
