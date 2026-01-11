using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Style;

namespace AetherSenseRedux.Pattern
{
    internal class UserCurvePatternType : IPatternType
    {
        public string Name => "UserCurve";

        public IPattern Create(PatternConfig config)
        {
            return new UserCurvePattern((UserCurvePatternConfig)config);
        }

        public PatternConfig DeserializeConfiguration(dynamic source)
        {
            List<ControlPoint> points = new();
            foreach (dynamic point in source.ControlPoints)
            {
                points.Add(new((double)point.Time, (double)point.Intensity));
            }

            return new UserCurvePatternConfig()
            {
                Duration = (long)source.Duration,
                Tension = (double)source.Tension,
                ControlPoints = points.ToArray()
            };
        }

        public void DrawSettings(PatternConfig config)
        {
            UserCurvePatternConfig pattern = (UserCurvePatternConfig)config;
            int duration = (int)pattern.Duration;
            if (ImGui.SliderInt("Duration (ms)", ref duration, 1, 30000))
            {
                pattern.Duration = duration;
            }

            double tension = (double)pattern.Tension;
            if (ImGui.SliderDouble("Tension (0..1)", ref tension, 0, 1))
            {
                pattern.Tension = tension;
            }

            ControlPoint[] controlPoints = new ControlPoint[pattern.ControlPoints.Length];
            for (int i = 0; i < controlPoints.Length; ++i) controlPoints[i] = new(pattern.ControlPoints[i]);
            bool pointsDirty = false;

            if (controlPoints.Length < 2)
            {
                Array.Resize(ref controlPoints, 2);
                controlPoints[0].Time = 0.0;
                controlPoints[1].Time = 1.0;
                pointsDirty = true;
            }

            for (int i = 0; i < controlPoints.Length; ++i)
            {
                double tsubn = controlPoints[i].Time;
                double isubn = controlPoints[i].Intensity;
                bool first = i <= 0;
                bool last = i >= controlPoints.Length - 1;

                ImGui.PushID($"Insert {i}");
                if (!first && ImGui.Button("Insert"))
                {
                    Array.Resize(ref controlPoints, controlPoints.Length + 1);
                    for (int j = controlPoints.Length - 1; j > i; --j) controlPoints[j] = controlPoints[j - 1];
                    controlPoints[i] = new ControlPoint
                    {
                        Time = ((first ? 0 : controlPoints[i - 1].Time) + controlPoints[i + 1].Time) / 2,
                        Intensity = ((first ? 0 : controlPoints[i - 1].Intensity) + controlPoints[i + 1].Intensity) / 2,
                    };
                    pointsDirty = true;
                }
                ImGui.PopID();

                ImGui.Indent();
                ImGui.BeginGroup();
                ImGui.Text(first ? "Start" : (last ? "End" : $"Point {i + 1}"));

                if (!first && !last)
                {
                    ImGui.SameLine();
                    ImGui.PushID($"Delete {i}");
                    if (ImGui.Button("Delete"))
                    {
                        for (int j = i; j < controlPoints.Length - 1; ++j)
                        {
                            controlPoints[j] = controlPoints[j + 1];
                        }
                        Array.Resize(ref controlPoints, controlPoints.Length - 1);
                        pointsDirty = true;
                        ImGui.PopID();
                        --i;
                        continue;
                    }
                    ImGui.PopID();
                }

                if (first || last) ImGui.BeginDisabled();
                ImGui.PushID($"Time {i}");
                if (ImGui.SliderDouble("Time", ref tsubn, first ? 0.0 : controlPoints[i - 1].Time, last ? 1.0 : controlPoints[i + 1].Time))
                {
                    controlPoints[i].Time = tsubn;
                    pointsDirty = true;
                }
                ImGui.PopID();
                if (first || last) ImGui.EndDisabled();

                ImGui.PushID($"Intensity {i}");
                if (ImGui.SliderDouble("Intensity", ref isubn, 0, 1))
                {
                    controlPoints[i].Intensity = isubn;
                    pointsDirty = true;
                }
                ImGui.PopID();

                ImGui.EndGroup();
                ImGui.Unindent();
            }

            if (pointsDirty)
            {
                pattern.ControlPoints = new ControlPoint[controlPoints.Length];
                for (int i = 0; i < controlPoints.Length; ++i) pattern.ControlPoints[i] = new ControlPoint(controlPoints[i]);
            }
        }

        public PatternConfig GetDefaultConfiguration()
        {
            return UserCurvePattern.GetDefaultConfiguration();
        }

    }

    internal class UserCurvePattern : IPattern
    {
        public DateTime Begins { get; set; }
        public DateTime Expires { get; set; }

        internal struct Coefficients
        {
            public double Time, Cubic, Quadratic, Linear, Constant;
        }

        private readonly Coefficients[] Coeffs; // TODO I should probably structure this.

        protected static Coefficients CalculateCoefficients(ControlPoint p0, ControlPoint p1, ControlPoint p2, ControlPoint p3, double Tension)
        {
            // This is based off of Charles Petzold's Canonical Splines implementation
            // https://charlespetzold.com/blog/2009/01/Canonical-Splines-in-WPF-and-Silverlight.html
            double slope1 = (1 - Tension) * (p2.Intensity - p0.Intensity);
            double slope2 = (1 - Tension) * (p3.Intensity - p1.Intensity);

            // Coefficients in `a*t^3 + b*t^2 + c*t + d`
            double a = slope1 + slope2 + 2 * p1.Intensity - 2 * p2.Intensity;
            double b = -2 * slope1 - slope2 - 3 * p1.Intensity + 3 * p2.Intensity;
            double c = slope1;
            double d = p1.Intensity;

            return new Coefficients()
            {
                Time = p1.Time,
                Constant = d,
                Linear = c,
                Quadratic = b,
                Cubic = a,
            };
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
                    if (i == 0)
                    {
                        Coeffs[i] = CalculateCoefficients(config.ControlPoints[0], config.ControlPoints[0],
                            config.ControlPoints[1], config.ControlPoints[2], config.Tension);
                    }
                    else if (i == n - 1)
                    {
                        Coeffs[i] = CalculateCoefficients(config.ControlPoints[i - 1], config.ControlPoints[i], config.ControlPoints[i + 1], config.ControlPoints[i + 1], config.Tension);
                    }
                    else
                    {
                        Coeffs[i] = CalculateCoefficients(config.ControlPoints[i - 1], config.ControlPoints[i], config.ControlPoints[i + 1], config.ControlPoints[i + 2], config.Tension);
                    }
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
