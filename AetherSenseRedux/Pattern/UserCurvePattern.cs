using System;
using Dalamud.Interface.Style;

namespace AetherSenseRedux.Pattern
{
    internal class UserCurvePattern : IPattern
    {
        public DateTime Begins { get; set; }
        public DateTime Expires { get; set; }

        internal struct Coefficients
        {
            public double Time, Cubic, Quadratic, Linear, Constant;
        }

        private readonly Coefficients[] Coeffs; // TODO I should probably structure this.

        private static double Quadratic(double v0, double m0, double v1, double m1)
        {
            return -3 * v0 + 3 * v1 - 2 * m0 - m1;
        }

        private static double Cubic(double v0, double m0, double v1, double m1)
        {
            return 2 * v0 + m0 - 2 * v1 + m1;
        }

        private static double Slope(ControlPoint[] points, int i)
        {
            if (i < 0 || i > points.Length - 1)
            {
                throw new IndexOutOfRangeException();
            }
            if (i == 0)
            {
                return (points[1].Intensity - points[0].Intensity) / points[1].Time;
            }
            else if (i == points.Length - 1)
            {
                return (points[i].Intensity - points[i - 1].Intensity) / (1.0 - points[i - 1].Time);
            }
            else
            {
                return (points[i + 1].Intensity - points[i - 1].Intensity) / (points[i + 1].Time - points[i - 1].Time);
            }
        }

        public UserCurvePattern(UserCurvePatternConfig config)
        {
            Begins = DateTime.UtcNow;
            Expires = Begins + TimeSpan.FromMilliseconds(config.Duration);

            if (config.ControlPoints.Length == 0)
            {
                Service.PluginLog.Warning("UserCurvePattern with 0 control points. Pattern will have no effect. This should be impossible; please check the UI code.");
                Coeffs = Array.Empty<Coefficients>();
            }
            else if (config.ControlPoints.Length == 1)
            {
                Service.PluginLog.Warning("UserCurvePattern with one control point. Pattern will be constant. This shouldn't be possible; please check the UI code.");

                Coeffs = [
                    new Coefficients {
                        Time = 0.0,
                        Cubic = 0.0,
                        Quadratic = 0.0,
                        Linear = 0.0,
                        Constant = config.ControlPoints[0].Intensity
                    }
                ];
            }
            else
            {
                // 1. Sort control points by time.
                Array.Sort(config.ControlPoints);

                // 2. Normalize time if necessary. (It shouldn't be.)
                int n = config.ControlPoints.Length - 1;
                double tStart = config.ControlPoints[0].Time;
                double tEnd = config.ControlPoints[n].Time;
                // TODO: I don't know if there's a better way to handle this. Insert fake control points? With what values? Constant slope extrapolations?
                // For now, it's simpler to just adjust all the time coords to be exactly 0..1.
                if (tStart != 0.0 || tEnd != 1.0)
                {
                    if (tStart != 0.0) Service.PluginLog.Warning("UserCurvePattern with t0 = {} instead of 0.0. Time will be normalized. This shouldn't be possible; please check the UI code.");
                    if (tEnd != 1.0) Service.PluginLog.Warning("UserCurvePattern with t(n-1) = {} instead of 1.0. Time will be normalized. This shouldn't be possible; please check the UI code.");

                    for (int i = 0; i <= n; ++i) config.ControlPoints[i].Time = (config.ControlPoints[i].Time - tStart) / (tEnd - tStart);
                }

                // 3. Calculate curvature slopes at each control point. These will be used for the spline calculation.
                double[] curveSlopes = new double[n + 1];
                curveSlopes[0] = (config.ControlPoints[1].Intensity - config.ControlPoints[0].Intensity) / config.ControlPoints[1].Time;
                for (int i = 1; i < n; ++i) curveSlopes[i] = (config.ControlPoints[i + 1].Intensity - config.ControlPoints[i - 1].Intensity) / (config.ControlPoints[i + 1].Time - config.ControlPoints[i - 1].Time) * (1 - config.Tension);
                curveSlopes[n] = (config.ControlPoints[n].Intensity - config.ControlPoints[n - 1].Intensity) / (1 - config.ControlPoints[n - 1].Time);

                // 4. Calculate the spline coefficients and stash them.
                Coeffs = new Coefficients[n];
                for (int i = 0; i < n; ++i)
                {
                    double v0 = config.ControlPoints[i].Intensity;
                    double m0 = curveSlopes[i];
                    double v1 = config.ControlPoints[i + 1].Intensity;
                    double m1 = curveSlopes[i + 1];

                    Coeffs[i] = new Coefficients
                    {
                        Time = config.ControlPoints[i].Time,
                        Constant = v0,
                        Linear = m0,
                        Quadratic = Quadratic(v0, m0, v1, m1),
                        Cubic = Cubic(v0, m0, v1, m1)
                    };
                }
            }
        }

        public double GetIntensityAtTime(DateTime time)
        {
            if (time < Begins) throw new PatternException("Too early to evaluate user curve pattern.");
            if (time > Expires) throw new PatternExpiredException();

            double t = (time - Begins) / (Expires - Begins);

            for (int i = 0; i < Coeffs.Length; ++i)
            {
                if (Coeffs[i].Time <= t && (i == Coeffs.Length - 1 || t <= Coeffs[i + 1].Time))
                {
                    t = (t - Coeffs[i].Time) / ((i == Coeffs.Length - 1 ? 1.0 : Coeffs[i + 1].Time) - Coeffs[i].Time);
                    double t2 = t * t;
                    double t3 = t2 * t;
                    return Math.Clamp(Coeffs[i].Cubic * t3 + Coeffs[i].Quadratic * t2 + Coeffs[i].Linear * t + Coeffs[i].Constant, 0.0, 1.0);
                }
            }

            throw new PatternException("Couldn't find subsegment of curve pattern! Did some weird math happen?");
        }

        public static PatternConfig GetDefaultConfiguration()
        {
            return new UserCurvePatternConfig();
        }
    }

    [Serializable]
    public class ControlPoint : IComparable, ICloneable
    {
        public double Time { get; set; } = 0.0;
        public double Intensity { get; set; } = 0.0;

        public ControlPoint(ControlPoint other)
        {
            this.Time = other.Time;
            this.Intensity = other.Intensity;
        }

        public ControlPoint(double time, double intensity)
        {
            this.Time = time;
            this.Intensity = intensity;
        }

        public ControlPoint()
        {
            this.Time = 0.0;
            this.Intensity = 0.0;
        }

        public int CompareTo(object? obj)
        {
            if (obj is ControlPoint other)
            {
                return Math.Sign(this.Time - other.Time);
            }
            else
            {
                return 1;
            }
        }

        public object Clone()
        {
            return new ControlPoint(this);
        }
    }

    [Serializable]
    public class UserCurvePatternConfig : PatternConfig
    {
        public override string Type { get; } = "UserCurve";
        public ControlPoint[] ControlPoints { get; set; } = [
            new ControlPoint { Time = 0.0, Intensity = 0.0 },
            new ControlPoint { Time = 0.5, Intensity = 1.0 },
            new ControlPoint { Time = 1.0, Intensity = 0.0 }
        ];

        public double Tension { get; set; } = 1;
    }
}
