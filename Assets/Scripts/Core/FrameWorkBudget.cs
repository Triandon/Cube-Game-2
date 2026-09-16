using System;
using System.Diagnostics;

namespace Core
{
    public sealed class FrameWorkBudget
    {
        private long startTimeStamp;
        private double budgetMs;
        private bool unlimited;

        public double BudgetMs => budgetMs;
        public double ElapsedMs => GetElapsedMilliseconds();
        public double RemainingMs => unlimited ? double.PositiveInfinity : 
            Math.Max(0.0, budgetMs - ElapsedMs);

        public bool hasTimeRemaining => unlimited || ElapsedMs < budgetMs;

        public void BeginFrame(int targetFps, double previousFrameMs, double maxBudgetFraction,
            double safteyMarginMs, double minBudgetMs, bool enabled)
        {
            startTimeStamp = Stopwatch.GetTimestamp();
            unlimited = !enabled;

            if (unlimited)
            {
                budgetMs = double.PositiveInfinity;
                return;
            }

            int clampedTargetFps = Math.Max(1, targetFps);
            double clampedBudgetFraction = Math.Max(0.05, maxBudgetFraction);
            double clampedSafteyMarginMs = Math.Max(0.0, safteyMarginMs);
            double clampedMinBudgetsMs = Math.Max(0.0, minBudgetMs);

            double targetedFrameMs = 1000.0 / clampedTargetFps;
            double maxBudgetMs = targetedFrameMs * clampedBudgetFraction;
            double availableFrameMs = targetedFrameMs - Math.Max(0.0, previousFrameMs) - clampedSafteyMarginMs;

            budgetMs = Math.Max(clampedMinBudgetsMs, Math.Min(Math.Max(clampedMinBudgetsMs, maxBudgetMs),
                availableFrameMs));
        }

        private double GetElapsedMilliseconds()
        {
            return (Stopwatch.GetTimestamp() - startTimeStamp) * 1000.0 / Stopwatch.Frequency;
        }

    }
}
